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

    // PostgreSQL LIKE/ILIKE treats % and _ as wildcards and uses backslash as the default escape character.
    // The repository passes ESCAPE '!' and doubles '!', so %, _, \ and ! in a search are all matched literally.
    [PostgresFact]
    public async Task SearchAsync_PostgreSql_Should_TreatSpecialCharactersAsLiteralText()
    {
        // Arrange: a disposable migrated database, like the other PostgreSQL tests (TECHSTORE_TEST_CONNECTION).
        await using var database = await PostgresDatabase.CreateAsync(migrate: true);
        await using var dbContext = database.CreateContext();
        var prefix = "Escape-";
        var customers = new[]
        {
            NewCustomer(prefix + "Percent%Mark", "0910000000"),
            NewCustomer(prefix + "Underscore_Mark", "0910000001"),
            NewCustomer(prefix + "Backslash\\Mark", "0910000002"),
            NewCustomer(prefix + "Bang!Mark", "0910000003"),
            NewCustomer(prefix + "PercentXMark", "0910000004"),
            NewCustomer(prefix + "UnderscoreXMark", "0910000005"),
            NewCustomer(prefix + "BackslashMark", "0910000006")
        };
        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync();
        var repository = new CustomerRepository(dbContext);

        foreach (var (fragment, expected) in new[]
        {
            ("Percent%", customers[0]),
            ("Underscore_", customers[1]),
            ("Backslash\\", customers[2]),
            ("Bang!", customers[3]),
            ("%", customers[0]),
            ("_", customers[1])
        })
        {
            // Act
            var (items, count) = await repository.SearchAsync(new CustomerSearchCriteria(fragment.ToLowerInvariant(), null, true, 1, 20));

            // Assert: only the customer that really contains the character, never the X / no-backslash look-alikes.
            count.Should().Be(1, $"'{fragment}' must be matched literally by PostgreSQL ILIKE");
            items.Should().ContainSingle().Which.Customer.Id.Should().Be(expected.Id);
        }
    }

    private static Customer NewCustomer(string name, string phone) => new()
    {
        FullName = name,
        Phone = phone
    };
}
