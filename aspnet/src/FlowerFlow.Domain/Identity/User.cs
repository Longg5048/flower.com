namespace FlowerFlow.Domain.Identity;

/// <summary>Application profile and role; Firebase remains the identity provider.</summary>
public sealed class User
{
    public int Id { get; set; }
    public required string FirebaseUid { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
