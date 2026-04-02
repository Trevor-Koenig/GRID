using GRID.Data;
using GRID.Models;

namespace GRID.Services
{
    /// <summary>
    /// Logging provider that mirrors every Critical-level log entry into the
    /// AuditLog table so production incidents are visible alongside other audit
    /// events without requiring access to Docker/server logs.
    /// </summary>
    public sealed class CriticalErrorAuditLoggerProvider(IServiceScopeFactory scopeFactory) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
            => new CriticalErrorAuditLogger(categoryName, scopeFactory);

        public void Dispose() { }
    }

    internal sealed class CriticalErrorAuditLogger(string category, IServiceScopeFactory scopeFactory) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Critical;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var entry = BuildEntry(category, eventId, formatter(state, exception), exception);

            _ = Task.Run(() => WriteEntryAsync(scopeFactory, entry));
        }

        // Internal so tests can exercise the DB write directly without Task.Run.
        internal static AuditLog BuildEntry(
            string category, EventId eventId, string message, Exception? exception)
        {
            var details = exception != null
                ? $"{message}\n\n{exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}"
                : message;

            const int maxLength = 4000;
            if (details.Length > maxLength)
                details = details[..maxLength];

            return new AuditLog
            {
                Action = "CriticalError",
                EntityType = category,
                EntityId = eventId.Id != 0 ? eventId.ToString() : null,
                Details = details,
                Timestamp = DateTime.UtcNow
            };
        }

        internal static async Task WriteEntryAsync(IServiceScopeFactory scopeFactory, AuditLog entry)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AuditLogs.Add(entry);
                await db.SaveChangesAsync();
            }
            catch
            {
                // Never let audit logging cascade into another failure.
            }
        }
    }
}
