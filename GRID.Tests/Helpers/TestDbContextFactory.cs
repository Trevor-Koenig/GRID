using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GRID.Tests.Helpers;

/// <summary>
/// Creates an isolated in-memory SQLite database for each test.
/// Implement IDisposable in your test class and call Dispose() to clean up.
/// </summary>
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    public TestDbContext Context { get; }

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new TestDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
