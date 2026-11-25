namespace TechStore.Shared.Requests;

public sealed record ReceiveWarrantyRequest(
    string? Code, string? OrderCode, Guid? CustomerId,
    string? IssueDescription, string? ContactName, string? ContactPhone);

public sealed record InspectWarrantyRequest(string? Outcome, string? Note, string? RejectionReason = null);
public sealed record MarkWarrantyRepairedRequest(string? Note = null);
public sealed record RejectWarrantyReplacementRequest(string? Note);
public sealed record ReplaceWarrantyDeviceRequest(Guid ReplacementDeviceId);
