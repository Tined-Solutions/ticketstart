namespace TicketeraOnline.Api.Services;

/// <summary>
/// Configuration options for HMAC-SHA256 reservation tokens used to protect
/// anonymous (guest) checkout endpoints from IDOR attacks.
/// </summary>
public class ReservationTokenOptions
{
    public const string SectionName = "Reservation";

    /// <summary>
    /// Secret key used to sign reservation identifiers. Must be at least 32 characters.
    /// </summary>
    public string TokenSecretKey { get; set; } = string.Empty;
}
