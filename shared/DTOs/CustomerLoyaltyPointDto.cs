namespace TechStore.Shared.DTOs;

public class CustomerLoyaltyPointDto
{
    public Guid Id { get; set; }
    public int PointsChange { get; set; }
    public int BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}
