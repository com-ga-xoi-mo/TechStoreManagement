using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;

namespace TechStore.UnitTests;

public class CustomerRepositoryPostgresTests
{
    [Fact]
    public void ILike_PostgreSqlProvider_Should_EmitExplicitEscapeClause()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;
        using var dbContext = new AppDbContext(options);

        var sql = dbContext.Customers
            .Where(customer => EF.Functions.ILike(customer.FullName, "%!%%", "!"))
            .ToQueryString();

        sql.Should().Contain("ESCAPE '!'");
    }

    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task SearchAsync_PostgreSql_Should_TreatSpecialCharactersAsLiteralText()
    {
        // Point this at a migrated test database. The transaction keeps its existing data unchanged.
        var connectionString = Environment.GetEnvironmentVariable("TECHSTORE_TEST_POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var dbContext = new AppDbContext(options);
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var prefix = $"Escape-{Guid.NewGuid():N}-";
        var phonePrefix = Random.Shared.Next(100_000_000, 1_000_000_000).ToString();
        var customers = new[]
        {
            NewCustomer(prefix + "Percent%Mark", phonePrefix + "0"),
            NewCustomer(prefix + "Underscore_Mark", phonePrefix + "1"),
            NewCustomer(prefix + "Backslash\\Mark", phonePrefix + "2"),
            NewCustomer(prefix + "Bang!Mark", phonePrefix + "3"),
            NewCustomer(prefix + "PercentXMark", phonePrefix + "4"),
            NewCustomer(prefix + "UnderscoreXMark", phonePrefix + "5")
        };
        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync();

        var repository = new CustomerRepository(dbContext);
        foreach (var (fragment, expected) in new[]
        {
            ("Percent%", customers[0]),
            ("Underscore_", customers[1]),
            ("Backslash\\", customers[2]),
            ("Bang!", customers[3])
        })
        {
            var (items, count) = await repository.SearchAsync(new CustomerSearchCriteria(
                (prefix + fragment).ToLowerInvariant(), null, true, 1, 20));

            count.Should().Be(1, $"{fragment} must be matched literally by PostgreSQL ILIKE");
            items.Should().ContainSingle().Which.Customer.Id.Should().Be(expected.Id);
        }

        await transaction.RollbackAsync();
    }

    private static Customer NewCustomer(string name, string phone) => new()
    {
        FullName = name,
        Phone = phone
    };
}
