namespace TechStore.Api.Common.Data;

public class DuplicateKeyException : Exception
{
    public string? ConstraintName { get; }

    public DuplicateKeyException(string? constraintName = null, string? message = null, Exception? innerException = null)
        : base(message ?? $"A duplicate key violation occurred{(constraintName != null ? $" on constraint '{constraintName}'" : "")}.", innerException)
    {
        ConstraintName = constraintName;
    }
}
