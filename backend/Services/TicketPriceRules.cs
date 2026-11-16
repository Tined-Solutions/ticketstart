namespace TicketeraOnline.Api.Services;

/// <summary>
/// Ticket prices are natural numbers: whole pesos, at least 1. No decimals
/// (what the buyer sees, what is charged in Mercado Pago and what is stored
/// must be the same number) and no zero (Mercado Pago rejects zero-priced
/// items, and the platform has no free-ticket flow).
///
/// Shared by every write path that sets a price: event creation, admin
/// add-ticket-type and admin replace-ticket-types.
/// </summary>
public static class TicketPriceRules
{
    public const string InvalidPriceMessage = "Ticket price must be a whole number greater than zero";

    public static bool IsValid(decimal price) => price >= 1 && price == decimal.Truncate(price);

    /// <exception cref="ArgumentException">When the price is not a natural number.</exception>
    public static void EnsureValid(decimal price, string? paramName = null)
    {
        if (!IsValid(price))
            throw new ArgumentException(InvalidPriceMessage, paramName);
    }
}
