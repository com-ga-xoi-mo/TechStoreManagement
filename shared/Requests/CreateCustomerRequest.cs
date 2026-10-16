namespace TechStore.Shared.Requests;

public class CreateCustomerRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
}
