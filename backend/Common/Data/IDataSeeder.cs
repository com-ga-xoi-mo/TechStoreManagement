namespace TechStore.Api.Common.Data;

public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
