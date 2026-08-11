namespace TicketeraOnline.Api.Models;

/// <summary>
/// Shared retry-state contract implemented by entities whose email delivery
/// is managed through IRetryableEmailSender.ProcessAsync.
/// </summary>
public interface IRetryableEmailRow
{
    string Status { get; set; }
    int Attempts { get; set; }
    int MaxAttempts { get; set; }
    string? LastError { get; set; }
    DateTime? LastAttemptAt { get; set; }
    string RecipientEmail { get; set; }
}
