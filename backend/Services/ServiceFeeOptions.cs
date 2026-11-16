namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service fee ("cargo por servicio") charged to the buyer on top of the ticket
/// subtotal. Bound to the <c>ServiceFee</c> configuration section, which is
/// REQUIRED at startup (fail-fast in Program.cs) so the fee is never silently 0.
///
/// The fee is collected through Mercado Pago as a separate preference item and
/// stored apart from the ticket amount (<c>Transaction.ServiceFee</c>), so it is
/// never counted as organizer revenue. It is not returned on admin refunds; it
/// IS returned on the automatic stock-failure refund, where the buyer receives
/// no tickets at all.
/// </summary>
public class ServiceFeeOptions
{
    public const string SectionName = "ServiceFee";

    /// <summary>Fee percentage over the ticket subtotal (e.g. 10 = 10%). Range 0–100.</summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Fee for a given ticket subtotal, rounded to cents (midpoint away from zero).
    /// The frontend mirrors this exact rule to show the breakdown before paying.
    /// </summary>
    public static decimal Calculate(decimal subtotal, decimal percentage) =>
        Math.Round(subtotal * percentage / 100m, 2, MidpointRounding.AwayFromZero);

    public decimal Calculate(decimal subtotal) => Calculate(subtotal, Percentage);
}
