using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Shared retry state machine for email delivery. Owns the per-row loop
/// (attempt++, success/failure recording, status flip) so both the new
/// EventNotificationDispatchService and the refactored PaymentService
/// share a single implementation.
///
/// The in-memory Resend retry (exponential backoff) lives separately in
/// EmailService.SendWithRetryAsync — this class manages the DB row state.
/// </summary>
public class RetryableEmailSender : IRetryableEmailSender
{
    private readonly ApplicationDbContext _context;

    public RetryableEmailSender(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task ProcessAsync<T>(
        IEnumerable<T> rows,
        Func<T, CancellationToken, Task> sendFunc,
        CancellationToken cancellationToken) where T : IRetryableEmailRow
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await sendFunc(row, cancellationToken);

                // Success: mark sent
                row.Attempts++;
                row.Status = "sent";
                row.LastError = null;
            }
            catch (Exception ex)
            {
                // Failure: record attempt and error
                row.Attempts++;
                row.LastError = ex.Message;
                row.LastAttemptAt = DateTime.UtcNow;

                if (row.Attempts >= row.MaxAttempts)
                {
                    row.Status = "exhausted";
                }
                // else remains at current status (Pending or whatever it was)
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
