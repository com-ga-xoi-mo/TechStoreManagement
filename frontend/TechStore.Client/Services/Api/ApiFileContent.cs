namespace TechStore.Client.Services.Api;

public sealed record ApiFileContent(string FieldName, string FileName, string ContentType, byte[] Content);
