using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechStore.Api.Analytics.Services;
using Microsoft.Extensions.Options;
using TechStore.Api.Assistant;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Ai;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Enums;

namespace TechStore.UnitTests;

public sealed class AssistantHttpTests
{
    [Fact]
    public async Task AuthenticationRolesAndBodyValidation()
    {
        await using var root = new IdentityApiFactory();
        var model = new AssistantTests.ModelFake();
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Configure<AssistantOptions>(o => { o.BaseUrl = "https://example.test/v1"; o.ApiKey = "secret"; o.Model = "model"; });
            services.RemoveAll<IChatModelClient>(); services.AddSingleton<IChatModelClient>(model);
            services.RemoveAll<IProductService>(); services.AddSingleton<IProductService, AssistantTests.ProductFake>();
            services.RemoveAll<IInventoryService>(); services.AddSingleton<IInventoryService, AssistantTests.StockFake>();
            services.RemoveAll<IAnalyticsService>(); services.AddSingleton<IAnalyticsService, AssistantTests.AnalyticsFake>();
        }));
        using var client = factory.CreateClient();
        var body = new { messages = new[] { new { role = "user", content = "hello" } } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/assistant/chat", body)).StatusCode);
        Assert.Empty(model.Requests);
        foreach (var role in Enum.GetValues<RoleType>())
        {
            if (role == RoleType.Admin) await root.LoginAsync(client);
            else
            {
                var user = IdentityMigrationPostgresTests.NewAdmin(); user.Username = role.ToString(); user.UserRoles.Single().Role.Name = role;
                factory.Services.GetRequiredService<FakeUserRepository>().Add(user);
                await root.LoginAsync(client, user.Username);
            }
            model.Replies.Enqueue(new("ok", []));
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/assistant/chat", body)).StatusCode);
        }
        model.Requests.Clear();
        object[] invalidBodies =
        [
            new { messages = new[] { new { role = "system", content = "bad" } } },
            new { messages = new[] { new { role = "tool", content = "bad" } } },
            new { messages = new[] { new { role = "assistant", content = "last" } } },
            new { messages = new[] { new { role = "user", content = "" } } },
            new { messages = new[] { new { role = "user", content = "   " } } },
            new { messages = new[] { new { role = "user", content = new string('x', 2001) } } },
            new { messages = new[] { new { role = (string?)null, content = "text" } } },
            new { messages = new[] { new { role = "user", content = (string?)null } } },
            new { messages = new object?[] { null } },
            new { messages = Enumerable.Repeat(new { role = "user", content = "x" }, 21) },
            new { messages = (object?)null }
        ];
        foreach (var invalid in invalidBodies)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/assistant/chat", invalid)).StatusCode);
        Assert.Empty(model.Requests);
        model.Fail = true;
        var failure = await client.PostAsJsonAsync("/api/assistant/chat", body);
        Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
        var failureText = await failure.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret", failureText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", failureText, StringComparison.OrdinalIgnoreCase);
        model.Fail = false; model.Block = true;
        var options = factory.Services.GetRequiredService<IOptions<AssistantOptions>>().Value;
        options.TimeoutSeconds = 1;
        Assert.Equal(HttpStatusCode.GatewayTimeout, (await client.PostAsJsonAsync("/api/assistant/chat", body)).StatusCode);
        options.ApiKey = "";
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/assistant/chat", body)).StatusCode);
    }
}
