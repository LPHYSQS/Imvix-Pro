using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace ImvixPro.Services
{
    public sealed class SingleInstanceService : IDisposable
    {
        private readonly string _mutexName;
        private readonly string _pipeName;
        private readonly Mutex _mutex;
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenerTask;
        private int _pendingActivations;

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

        public event Action? ActivationRequested;

        public bool ConsumePendingActivation()
        {
            return Interlocked.Exchange(ref _pendingActivations, 0) > 0;
        }

        public void SignalExistingInstance()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                    client.Connect(250);
                    using var writer = new StreamWriter(client) { AutoFlush = true };
                    writer.WriteLine("activate");
                    return;
                }
                catch (Exception ex)
                {
                    AppServices.Logger.LogDebug(nameof(SingleInstanceService), $"Failed to signal the existing instance on attempt {attempt + 1}.", ex);
                    Thread.Sleep(150);
                }
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
                    _ = await reader.ReadLineAsync().ConfigureAwait(false);
                    RaiseActivationRequested();
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

        private void RaiseActivationRequested()
        {
            var handler = ActivationRequested;
            if (handler is null)
            {
                Interlocked.Exchange(ref _pendingActivations, 1);
                return;
            }

            handler.Invoke();
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
    }
}
