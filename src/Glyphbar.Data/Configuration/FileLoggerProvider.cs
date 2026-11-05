using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Data.Configuration;

/// <summary>Small daily-rolling file logger. One background writer; keeps the last 7 logs.</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly BlockingCollection<string> _queue = new(4096);
    private readonly Thread _writer;
    private readonly string _directory;

    public FileLoggerProvider(AppPaths paths)
    {
        _directory = paths.Logs;
        Prune();
        _writer = new Thread(Pump) { IsBackground = true, Name = "Glyphbar log writer" };
        _writer.Start();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    private void Prune()
    {
        try
        {
            foreach (var old in new DirectoryInfo(_directory).GetFiles("glyphbar-*.log")
                         .OrderByDescending(f => f.Name).Skip(7))
                old.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Failing to tidy old logs must never stop the app.
            _ = e;
        }
    }

    private void Pump()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                string file = Path.Combine(_directory, $"glyphbar-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _ = e;
            }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(2));
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        private readonly string _shortCategory = category[(category.LastIndexOf('.') + 1)..];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || owner._queue.IsAddingCompleted) return;
            string stamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string line = $"{stamp} {logLevel,-11} {_shortCategory}: {formatter(state, exception)}";
            if (exception is not null) line += Environment.NewLine + exception;
            owner._queue.TryAdd(line);
        }
    }
}
