using GRID.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Helpers;

/// <summary>
/// Creates an isolated in-memory SQLite ApplicationDbContext for service-level tests.
/// Use when the code under test expects <see cref="ApplicationDbContext"/> directly.
/// </summary>
public sealed class TestApplicationDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    public TestApplicationDbContext Context { get; }

    public TestApplicationDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new TestApplicationDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
