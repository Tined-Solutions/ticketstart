using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Shared retry engine for email delivery. Owns the per-row state machine
/// (Attempts++, Status flip, LastError recording) so that both the new
/// EventNotificationDispatchService and the refactored PaymentService
/// share one implementation — zero duplication.
/// </summary>
public interface IRetryableEmailSender
{
    /// <summary>
    /// Iterates over rows, calls sendFunc per row, and records success/failure.
    /// After all rows are processed, saves changes once.
    /// </summary>
    /// <typeparam name="T">Any entity implementing IRetryableEmailRow</typeparam>
    /// <param name="rows">Rows to process</param>
    /// <param name="sendFunc">Delegated send action — throws on failure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ProcessAsync<T>(
        IEnumerable<T> rows,
        Func<T, CancellationToken, Task> sendFunc,
        CancellationToken cancellationToken) where T : IRetryableEmailRow;
}
