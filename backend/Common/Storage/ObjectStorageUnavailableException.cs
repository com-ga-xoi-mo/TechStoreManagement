namespace TechStore.Api.Common.Storage;

public sealed class ObjectStorageUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
