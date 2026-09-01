using System.Security.Cryptography;

namespace TicketeraOnline.Api.Helpers;

/// <summary>
/// Cryptographically secure temporary-password generator (AUM-003, D9).
/// Static helper by house precedent (HmacHelper/LogRedactor): the generated
/// credential is 12–16 alphanumeric characters — comfortably above the min-8
/// login policy — using a CSPRNG (RandomNumberGenerator), never a seeded PRNG.
/// </summary>
public static class PasswordGenerator
{
    private const int MinLength = 12;
    private const int MaxLength = 16; // inclusive — GetInt32's upper bound is exclusive, hence 17
    private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Generates a random alphanumeric password of 12–16 characters.
    /// Callers MUST treat the return value as a one-time secret: persist only
    /// its BCrypt hash and hand the cleartext to the user exactly once.
    /// </summary>
    public static string Generate()
    {
        var length = RandomNumberGenerator.GetInt32(MinLength, MaxLength + 1);
        return RandomNumberGenerator.GetString(AllowedChars, length);
    }
}
