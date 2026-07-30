namespace TicketeraOnline.Api.Services;

public interface ITurnstileService
{
    Task<bool> VerifyTokenAsync(string token, CancellationToken cancellationToken = default);
}
