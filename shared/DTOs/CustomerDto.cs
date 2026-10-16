namespace TechStore.Shared.DTOs;

public class CustomerDto : CustomerSummaryDto
{
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
