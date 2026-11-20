using System.Net;

namespace TechStore.Client.Services.Api;

public sealed record ApiResponse<TResponse>(HttpStatusCode StatusCode, TResponse Data)
{
    public bool IsCreated => StatusCode == HttpStatusCode.Created;
    public bool IsOk => StatusCode == HttpStatusCode.OK;
}
