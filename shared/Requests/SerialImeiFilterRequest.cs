using TechStore.Shared.Enums;

namespace TechStore.Shared.Requests;

public class SerialImeiFilterRequest
{
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public SerialImeiStatus? Status { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
