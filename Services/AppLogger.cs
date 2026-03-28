using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ImvixPro.Services
{
    public sealed class AppLogger
    {
        private readonly object _gate = new();
        private readonly string _logDirectoryPath;

        public AppLogger(string? logDirectoryPath = null)
        {
            _logDirectoryPath = string.IsNullOrWhiteSpace(logDirectoryPath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppIdentity.InternalName,
                    "Logs")
                : logDirectoryPath;
        }

        public void LogDebug(string context, string message, Exception? exception = null)
        {
            Write("DEBUG", context, message, exception);
        }

        public void LogWarning(string context, string message, Exception? exception = null)
        {
            Write("WARN", context, message, exception);
        }

        public void LogError(string context, Exception exception, string? message = null)
        {
            Write("ERROR", context, message ?? exception.Message, exception);
        }

        private void Write(string level, string context, string message, Exception? exception)
        {
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            var builder = new StringBuilder()
                .Append('[').Append(timestamp).Append("] [").Append(level).Append("] ")
                .Append(context)
                .Append(": ")
                .Append(message);

            if (exception is not null)
            {
                builder.AppendLine()
                    .Append(exception);
            }

            var entry = builder.ToString();
            Trace.WriteLine(entry);

            try
            {
                lock (_gate)
                {
                    Directory.CreateDirectory(_logDirectoryPath);
                    var logPath = Path.Combine(_logDirectoryPath, $"{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(logPath, entry + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception logFailure)
            {
                Trace.WriteLine($"[WARN] AppLogger: failed to persist log entry. {logFailure}");
            }
        }
    }
}
