namespace TechStore.Api.Common.Data;

public sealed class ForeignKeyViolationException : Exception
{
    public string? ConstraintName { get; }

    public ForeignKeyViolationException(string? constraintName = null, string? message = null, Exception? innerException = null)
        : base(message ?? "A foreign-key constraint prevented the operation.", innerException)
    {
        ConstraintName = constraintName;
    }
}
