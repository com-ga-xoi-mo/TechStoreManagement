using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechStore.Api.Common.Data;

namespace TechStore.UnitTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TECHSTORE_TEST_CONNECTION")))
            Skip = "Set TECHSTORE_TEST_CONNECTION to run against a disposable PostgreSQL database.";
    }
}

/// <summary>Creates and drops only a generated test database; never changes the configured source database.</summary>
public sealed class PostgresDatabase : IAsyncDisposable
{
    private readonly string _adminConnection;
    private readonly string _databaseName = "techstore_category_test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; }

    private PostgresDatabase(string connection)
    {
        var builder = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false };
        _adminConnection = builder.ConnectionString;
        builder.Database = _databaseName;
        ConnectionString = builder.ConnectionString;
    }

    public static async Task<PostgresDatabase> CreateAsync()
    {
        var database = new PostgresDatabase(Environment.GetEnvironmentVariable("TECHSTORE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("TECHSTORE_TEST_CONNECTION is required."));
        await using var admin = new NpgsqlConnection(database._adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database._databaseName}\"", admin);
        await command.ExecuteNonQueryAsync();
        try
        {
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }
}
