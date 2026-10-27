namespace TechStore.Api.Common.Data;

public sealed class CheckConstraintViolationException : Exception
{
    public string? ConstraintName { get; }

    public CheckConstraintViolationException(string? constraintName = null, string? message = null, Exception? innerException = null)
        : base(message ?? "A check constraint prevented the operation.", innerException)
    {
        ConstraintName = constraintName;
    }
}
