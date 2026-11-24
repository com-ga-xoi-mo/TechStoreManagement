namespace TechStore.Shared.Requests;

public record ReturnLineRequest(Guid OrderItemId, int Quantity, Guid? SerialImeiId);
public record CreateReturnRequest(Guid RequestId, Guid OrderId, string Reason, List<ReturnLineRequest> Lines);
public record SettleReturnRequest(string Method);
public record ReconcileReturnRequest(string BankReference, decimal Amount, bool Paid, DateTimeOffset? PaidAt = null,
    DateTimeOffset? VerifiedLegacyApprovalAt = null);
public record InspectReturnRequest(Guid OperationId, int Quantity, string Outcome);
public record LinkExchangeRequest(Guid ReplacementOrderId);
