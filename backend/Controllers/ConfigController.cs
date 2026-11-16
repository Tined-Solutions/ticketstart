using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Public, read-only client configuration. Lets the frontend show the price
/// breakdown before reserving without duplicating backend business values.
/// </summary>
[ApiController]
[Route("api/config")]
[AllowAnonymous]
public class ConfigController : ControllerBase
{
    private readonly ServiceFeeOptions _serviceFee;

    public ConfigController(IOptions<ServiceFeeOptions> serviceFeeOptions)
    {
        _serviceFee = serviceFeeOptions.Value;
    }

    /// <summary>
    /// Checkout settings: the service fee percentage applied over the ticket subtotal.
    /// </summary>
    [HttpGet("checkout")]
    [ProducesResponseType(typeof(CheckoutConfigResponse), StatusCodes.Status200OK)]
    public IActionResult GetCheckoutConfig() =>
        Ok(new CheckoutConfigResponse { ServiceFeePercentage = _serviceFee.Percentage });
}

public class CheckoutConfigResponse
{
    public decimal ServiceFeePercentage { get; set; }
}
