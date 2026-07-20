namespace TicketeraOnline.Api.Models;

public class TurnstileOptions
{
    public const string SectionName = "Turnstile";
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}
