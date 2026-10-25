using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests.Catalog;

public class CategoryPostgresTests
{
    [PostgresFact]
    public async Task Put_Should_Persist_All_Fields_Reject_Invalid_Changes_And_Describe_Swagger()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        var original = new Category { Name = "Original", Slug = "original", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var parent = new Category { Name = "Parent", Slug = "parent", IsActive = false };
        await SeedAsync(database, original, parent);
        await using var host = CreateHost(database);
        await host.StartAsync();
        Authenticate(host);
        var before = DateTime.UtcNow;

        // Act
        using var success = await host.Client.PutAsJsonAsync($"/api/categories/{original.Id}", new UpdateCategoryRequest
        {
            Name = "  Điện thoại Đẹp  ", Slug = null, Description = "  Updated description  ",
            ParentId = parent.Id, IsActive = false
        });

        // Assert
        success.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await success.Content.ReadFromJsonAsync<CategoryDto>())!;
        dto.Name.Should().Be("Điện thoại Đẹp");
        dto.Slug.Should().Be("dien-thoai-dep");
        dto.Description.Should().Be("Updated description");
        dto.ParentId.Should().Be(parent.Id);
        dto.IsActive.Should().BeFalse();
        dto.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        dto.UpdatedAt.Value.Kind.Should().Be(DateTimeKind.Utc);
        await using (var fresh = database.CreateContext())
        {
            var persisted = (await fresh.Categories.FindAsync(original.Id))!;
            persisted.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers().Excluding(c => c.UpdatedAt));
            // PostgreSQL timestamptz stores microseconds; DateTime.UtcNow has 100 ns ticks.
            persisted.UpdatedAt!.Value.Should().BeCloseTo(dto.UpdatedAt.Value, TimeSpan.FromMicroseconds(1));
            persisted.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromMicroseconds(1));
        }
        using var rejected = await host.Client.PutAsJsonAsync($"/api/categories/{original.Id}", new
        {
            Name = "Invalid mutation", Slug = "changed", ParentId = original.Id, IsActive = true
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using (var fresh = database.CreateContext())
        {
            var persisted = (await fresh.Categories.FindAsync(original.Id))!;
            persisted.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers().Excluding(c => c.UpdatedAt));
            persisted.UpdatedAt!.Value.Should().BeCloseTo(dto.UpdatedAt.Value, TimeSpan.FromMicroseconds(1));
        }
        using var missing = await host.Client.PutAsJsonAsync($"/api/categories/{Guid.NewGuid()}", new { Name = "Missing", IsActive = true });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var missingState = await host.Client.PutAsJsonAsync($"/api/categories/{original.Id}", new { Name = "No state" });
        missingState.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var swagger = JsonDocument.Parse(await host.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = swagger.RootElement.GetProperty("paths").GetProperty("/api/categories/{id}").GetProperty("put");
        foreach (var code in new[] { "200", "400", "401", "403", "404", "409" })
            operation.GetProperty("responses").TryGetProperty(code, out _).Should().BeTrue();
    }

    [PostgresFact]
    public async Task Put_Should_Return_409_And_Roll_Back_When_Create_Claims_Slug_After_Precheck()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        var original = new Category { Name = "Original", Slug = "original", Description = "Keep me" };
        await SeedAsync(database, original);
        async Task ClaimSlug()
        {
            await using var competing = database.CreateContext();
            var created = await new CategoryService(new CategoryRepository(competing), new UnitOfWork(competing))
                .CreateAsync(new CreateCategoryRequest { Name = "Concurrent category", Slug = "claimed" });
            created.IsSuccess.Should().BeTrue();
        }
        await using var host = CreateHost(database, services => services.AddScoped<ICategoryRepository>(provider =>
            new SlugRaceRepository(new CategoryRepository(provider.GetRequiredService<AppDbContext>()), ClaimSlug)));
        await host.StartAsync();
        Authenticate(host);

        // Act
        using var response = await host.Client.PutAsJsonAsync($"/api/categories/{original.Id}",
            new { Name = "Must roll back", Slug = "claimed", Description = "Changed", IsActive = false });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("23505").And.NotContain("idx_categories_slug").And.NotContain("INSERT");
        await using var fresh = database.CreateContext();
        var persisted = (await fresh.Categories.FindAsync(original.Id))!;
        persisted.Name.Should().Be("Original");
        persisted.Slug.Should().Be("original");
        persisted.Description.Should().Be("Keep me");
        persisted.IsActive.Should().BeTrue();
        persisted.UpdatedAt.Should().BeNull();
        (await fresh.Categories.CountAsync(c => c.Slug == "claimed")).Should().Be(1);
    }

    [PostgresFact]
    public async Task Put_Should_Prevent_Concurrent_Reciprocal_Reparenting()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        var first = new Category { Name = "First", Slug = "first" };
        var second = new Category { Name = "Second", Slug = "second" };
        await SeedAsync(database, first, second);
        await using var host = CreateHost(database);
        await host.StartAsync();
        Authenticate(host);
        await using var gateContext = database.CreateContext();
        var gate = new UnitOfWork(gateContext);
        await gate.BeginTransactionAsync();
        await new CategoryRepository(gateContext).AcquireHierarchyUpdateLockAsync();

        // Act
        var firstUpdate = host.Client.PutAsJsonAsync($"/api/categories/{first.Id}",
            new { Name = "First", Slug = "first", ParentId = second.Id, IsActive = true });
        var secondUpdate = host.Client.PutAsJsonAsync($"/api/categories/{second.Id}",
            new { Name = "Second", Slug = "second", ParentId = first.Id, IsActive = true });
        var eitherUpdate = Task.WhenAny(firstUpdate, secondUpdate);
        (await Task.WhenAny(eitherUpdate, Task.Delay(150))).Should().NotBe(eitherUpdate);
        await gate.CommitAsync();
        var responses = await Task.WhenAll(firstUpdate, secondUpdate).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        try
        {
            responses.Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });
            var rejected = responses.Single(r => r.StatusCode == HttpStatusCode.BadRequest);
            (await rejected.Content.ReadAsStringAsync()).Should().Contain("parentId");
            await using var fresh = database.CreateContext();
            var categories = await fresh.Categories.ToListAsync();
            categories.Count(c => c.ParentId is null).Should().Be(1);
            categories.Count(c => c.ParentId is not null).Should().Be(1);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [PostgresFact]
    public async Task Put_Should_Cancel_Lock_Wait_Without_Persisting_Changes()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        var original = new Category { Name = "Original", Slug = "original" };
        await SeedAsync(database, original);
        await using var host = CreateHost(database);
        await host.StartAsync();
        Authenticate(host);
        await using var gateContext = database.CreateContext();
        var gate = new UnitOfWork(gateContext);
        await gate.BeginTransactionAsync();
        await new CategoryRepository(gateContext).AcquireHierarchyUpdateLockAsync();
        using var cancellation = new CancellationTokenSource();

        // Act
        var update = host.Client.PutAsJsonAsync($"/api/categories/{original.Id}",
            new { Name = "Cancelled", IsActive = false }, cancellation.Token);
        (await Task.WhenAny(update, Task.Delay(150))).Should().NotBe(update);
        cancellation.Cancel();

        // Assert
        await FluentActions.Awaiting(async () => await update.WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().ThrowAsync<OperationCanceledException>();
        await gate.RollbackAsync();
        await using var fresh = database.CreateContext();
        var verifier = new UnitOfWork(fresh);
        await verifier.BeginTransactionAsync();
        await new CategoryRepository(fresh).AcquireHierarchyUpdateLockAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await verifier.RollbackAsync();
        var persisted = (await fresh.Categories.FindAsync(original.Id))!;
        persisted.Name.Should().Be("Original");
        persisted.IsActive.Should().BeTrue();
        persisted.UpdatedAt.Should().BeNull();
    }

    [PostgresFact]
    public async Task Delete_Should_Remove_Only_Empty_Categories_And_Map_Integrity_Conflicts()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        var empty = new Category { Name = "Empty", Slug = "empty" };
        var parent = new Category { Name = "Parent", Slug = "parent" };
        var child = new Category { Name = "Child", Slug = "child", ParentId = parent.Id };
        var withProduct = new Category { Name = "With Product", Slug = "with-product" };
        await SeedAsync(database, empty, parent, child, withProduct);
        await using (var context = database.CreateContext())
        {
            context.Products.Add(new Product
            {
                CategoryId = withProduct.Id,
                Name = "Test product",
                Sku = "delete-test-sku",
                Brand = "Test",
                BasePrice = 100,
                CostPrice = 50
            });
            await context.SaveChangesAsync();
        }
        await using var host = CreateHost(database);
        await host.StartAsync();
        Authenticate(host);

        // Act / Assert: empty category is deleted
        using var success = await host.Client.DeleteAsync($"/api/categories/{empty.Id}");
        success.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using (var fresh = database.CreateContext())
            (await fresh.Categories.FindAsync(empty.Id)).Should().BeNull();

        // Act / Assert: subcategory protection returns 400
        using var subcategoryBlocked = await host.Client.DeleteAsync($"/api/categories/{parent.Id}");
        subcategoryBlocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await subcategoryBlocked.Content.ReadAsStringAsync()).Should()
            .Contain("Cannot delete category that contains subcategories. Move or delete subcategories first.");

        // Act / Assert: product protection returns 409
        using var productBlocked = await host.Client.DeleteAsync($"/api/categories/{withProduct.Id}");
        productBlocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await productBlocked.Content.ReadAsStringAsync()).Should()
            .Contain("Cannot delete category that contains associated products. Reassign or delete associated products first.");

        // Act / Assert: missing target returns 404
        using var missing = await host.Client.DeleteAsync($"/api/categories/{Guid.NewGuid()}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert: rejected targets remain and DELETE is documented
        await using (var fresh = database.CreateContext())
        {
            (await fresh.Categories.FindAsync(parent.Id)).Should().NotBeNull();
            (await fresh.Categories.FindAsync(withProduct.Id)).Should().NotBeNull();
        }
        using var swagger = JsonDocument.Parse(await host.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = swagger.RootElement.GetProperty("paths").GetProperty("/api/categories/{id}").GetProperty("delete");
        foreach (var code in new[] { "204", "400", "401", "403", "404", "409" })
            operation.GetProperty("responses").TryGetProperty(code, out _).Should().BeTrue();
    }

    private static async Task SeedAsync(PostgresDatabase database, params Category[] categories)
    {
        await using var context = database.CreateContext();
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();
    }

    private static CategoryApiHost CreateHost(PostgresDatabase database, Action<IServiceCollection>? additional = null) => new(services =>
    {
        services.AddScoped(_ => database.CreateContext());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryService, CategoryService>();
        additional?.Invoke(services);
    });

    private static void Authenticate(CategoryApiHost host) => host.Client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", host.Token());

    private sealed class SlugRaceRepository(ICategoryRepository inner, Func<Task> claimSlug) : ICategoryRepository
    {
        public async Task<bool> ExistsBySlugExceptIdAsync(string slug, Guid excludedId, CancellationToken cancellationToken = default)
        {
            var exists = await inner.ExistsBySlugExceptIdAsync(slug, excludedId, cancellationToken);
            if (!exists) await claimSlug();
            return exists;
        }
        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<Category?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetTrackedByIdAsync(id, cancellationToken);
        public Task<bool> HasSubCategoriesAsync(Guid id, CancellationToken cancellationToken = default) => inner.HasSubCategoriesAsync(id, cancellationToken);
        public Task<bool> HasProductsAsync(Guid id, CancellationToken cancellationToken = default) => inner.HasProductsAsync(id, cancellationToken);
        public Task<IReadOnlyList<CategoryTreeRow>> GetAllAsync(CancellationToken cancellationToken = default) => inner.GetAllAsync(cancellationToken);
        public Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default) => inner.ExistsBySlugAsync(slug, cancellationToken);
        public Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetDescendantIdsAsync(id, cancellationToken);
        public Task AcquireHierarchyUpdateLockAsync(CancellationToken cancellationToken = default) => inner.AcquireHierarchyUpdateLockAsync(cancellationToken);
        public void Add(Category category) => inner.Add(category);
        public void Delete(Category category) => inner.Delete(category);
    }
    [PostgresFact]
    public async Task Repository_Should_Track_Only_Update_Reads_And_Exclude_Own_Slug()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var category = new Category { Name = "Existing", Slug = "existing", IsActive = false };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new CategoryRepository(context);

        // Act / Assert
        (await repository.GetByIdAsync(category.Id)).Should().NotBeNull();
        context.ChangeTracker.Entries().Should().BeEmpty();
        var tracked = await repository.GetTrackedByIdAsync(category.Id);
        context.Entry(tracked!).State.Should().Be(EntityState.Unchanged);
        (await repository.ExistsBySlugExceptIdAsync(" EXISTING ", category.Id)).Should().BeFalse();
        (await repository.ExistsBySlugExceptIdAsync(" EXISTING ", Guid.NewGuid())).Should().BeTrue();
        tracked!.Name = "Persisted";
        await new UnitOfWork(context).SaveChangesAsync();
        await using var freshContext = database.CreateContext();
        (await freshContext.Categories.FindAsync(category.Id))!.Name.Should().Be("Persisted");
    }

    [PostgresFact]
    public async Task Repository_Should_Return_All_Descendants_Without_Tracking()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var root = new Category { Name = "Root", Slug = "root" };
        var child = new Category { Name = "Child", Slug = "child", ParentId = root.Id, IsActive = false };
        var leaf = new Category { Name = "Leaf", Slug = "leaf", ParentId = child.Id };
        var unrelated = new Category { Name = "Other", Slug = "other" };
        context.Categories.AddRange(root, child, leaf, unrelated);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // Act
        var descendants = await new CategoryRepository(context).GetDescendantIdsAsync(root.Id);

        // Assert
        descendants.Should().BeEquivalentTo(new[] { child.Id, leaf.Id });
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [PostgresFact]
    public async Task Repository_Should_Serialize_Hierarchy_Locks_And_Require_Transaction()
    {
        // Arrange
        await using var database = await PostgresDatabase.CreateAsync();
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstRepository = new CategoryRepository(first);
        var secondRepository = new CategoryRepository(second);
        var firstUnitOfWork = new UnitOfWork(first);
        var secondUnitOfWork = new UnitOfWork(second);
        await FluentActions.Awaiting(() => firstRepository.AcquireHierarchyUpdateLockAsync())
            .Should().ThrowAsync<InvalidOperationException>();
        await firstUnitOfWork.BeginTransactionAsync();
        await secondUnitOfWork.BeginTransactionAsync();
        await firstRepository.AcquireHierarchyUpdateLockAsync();

        // Act / Assert
        var waiting = secondRepository.AcquireHierarchyUpdateLockAsync();
        (await Task.WhenAny(waiting, Task.Delay(150))).Should().NotBe(waiting);
        await firstUnitOfWork.CommitAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await secondUnitOfWork.RollbackAsync();
        await firstUnitOfWork.BeginTransactionAsync();
        await firstRepository.AcquireHierarchyUpdateLockAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await firstUnitOfWork.RollbackAsync();
    }
}
