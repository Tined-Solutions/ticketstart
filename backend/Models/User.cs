namespace TicketeraOnline.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ICollection<Event> OrganizedEvents { get; set; } = new List<Event>();
}
