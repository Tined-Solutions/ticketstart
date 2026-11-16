using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

public class ConfigControllerTests
{
    [Fact]
    public void GetCheckoutConfig_ReturnsConfiguredServiceFeePercentage()
    {
        var controller = new ConfigController(Options.Create(new ServiceFeeOptions { Percentage = 10m }));

        var result = Assert.IsType<OkObjectResult>(controller.GetCheckoutConfig());
        var body = Assert.IsType<CheckoutConfigResponse>(result.Value);

        Assert.Equal(10m, body.ServiceFeePercentage);
    }
}
