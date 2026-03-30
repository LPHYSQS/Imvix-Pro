using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    public readonly record struct AppActivationRequest(bool IsActivationRequested, IReadOnlyList<string> Paths)
    {
        public static AppActivationRequest None => new(false, Array.Empty<string>());

        public static AppActivationRequest Activate(IEnumerable<string>? paths)
        {
            return new AppActivationRequest(true, NormalizePaths(paths));
        }

        private static IReadOnlyList<string> NormalizePaths(IEnumerable<string>? paths)
        {
            return paths?
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
        }
    }

    public sealed class SingleInstanceService : IDisposable
    {
        private readonly string _mutexName;
        private readonly string _pipeName;
        private readonly Mutex _mutex;
        private readonly CancellationTokenSource _cts = new();
        private readonly object _activationGate = new();
        private Task? _listenerTask;
        private AppActivationRequest _pendingActivation = AppActivationRequest.None;

        public SingleInstanceService(string appId)
        {
            _mutexName = $"{appId}.SingleInstance";
            _pipeName = $"{appId}.ActivationPipe";
            _mutex = new Mutex(true, _mutexName, out var createdNew);
            IsFirstInstance = createdNew;

            if (IsFirstInstance)
            {
                _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
            }
        }

        public bool IsFirstInstance { get; }

        public event Action<AppActivationRequest>? ActivationRequested;

        public AppActivationRequest ConsumePendingActivation()
        {
            lock (_activationGate)
            {
                var pending = _pendingActivation;
                _pendingActivation = AppActivationRequest.None;
                return pending;
            }
        }

        public void SignalExistingInstance(IEnumerable<string>? startupPaths = null)
        {
            var payload = SerializeActivationRequest(startupPaths);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                    client.Connect(250);
                    using var writer = new StreamWriter(client) { AutoFlush = true };
                    writer.WriteLine(payload);
                    return;
                }
                catch (Exception ex)
                {
                    AppServices.Logger.LogDebug(nameof(SingleInstanceService), $"Failed to signal the existing instance on attempt {attempt + 1}.", ex);
                    Thread.Sleep(150);
                }
            }
        }

        internal static string SerializeActivationRequest(IEnumerable<string>? startupPaths)
        {
            var request = AppActivationRequest.Activate(startupPaths);
            return JsonSerializer.Serialize(new ActivationPayload
            {
                Paths = request.Paths.ToArray()
            });
        }

        internal static AppActivationRequest DeserializeActivationRequest(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload) ||
                string.Equals(payload.Trim(), "activate", StringComparison.OrdinalIgnoreCase))
            {
                return AppActivationRequest.Activate(Array.Empty<string>());
            }

            try
            {
                var activationPayload = JsonSerializer.Deserialize<ActivationPayload>(payload);
                return AppActivationRequest.Activate(activationPayload?.Paths);
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(SingleInstanceService), "Failed to deserialize the activation payload. Falling back to a plain activation request.", ex);
                return AppActivationRequest.Activate(Array.Empty<string>());
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server);
                    var payload = await reader.ReadLineAsync().ConfigureAwait(false);
                    RaiseActivationRequested(DeserializeActivationRequest(payload));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppServices.Logger.LogDebug(nameof(SingleInstanceService), "Activation pipe listener hit an error and will keep listening.", ex);
                }
            }
        }

        private void RaiseActivationRequested(AppActivationRequest request)
        {
            var handler = ActivationRequested;
            if (handler is null)
            {
                lock (_activationGate)
                {
                    _pendingActivation = MergeRequests(_pendingActivation, request);
                }

                return;
            }

            handler.Invoke(request);
        }

        internal static AppActivationRequest MergeRequests(AppActivationRequest existing, AppActivationRequest next)
        {
            if (!existing.IsActivationRequested)
            {
                return next;
            }

            if (!next.IsActivationRequested)
            {
                return existing;
            }

            return AppActivationRequest.Activate(existing.Paths.Concat(next.Paths));
        }

        public void Dispose()
        {
            if (IsFirstInstance)
            {
                _cts.Cancel();
            }

            _cts.Dispose();

            if (IsFirstInstance)
            {
                try
                {
                    _listenerTask?.Wait(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    AppServices.Logger.LogDebug(nameof(SingleInstanceService), "Listener shutdown did not complete within the expected time window.", ex);
                }
            }

            if (IsFirstInstance)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (Exception ex)
                {
                    AppServices.Logger.LogDebug(nameof(SingleInstanceService), "Failed to release the single-instance mutex during disposal.", ex);
                }
            }

            _mutex.Dispose();
        }

        private sealed class ActivationPayload
        {
            public string[] Paths { get; set; } = Array.Empty<string>();
        }
    }
}
