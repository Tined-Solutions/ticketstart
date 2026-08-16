using Xunit;

namespace TicketeraOnline.Api.Services.Templates.Tests;

/// <summary>
/// Unit tests for EventDateChangeTemplate.Render.
/// Validates spec EDC-003: rendered output contains event name, old date,
/// new date, and refund contact email.
/// </summary>
public class EventDateChangeTemplateTests
{
    [Fact]
    public void Render_ContainsEventName()
    {
        var html = EventDateChangeTemplate.Render(
            "Rock Fest",
            new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc),
            "reembolsos@ticketera.com");

        Assert.Contains("Rock Fest", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_ContainsOldDate()
    {
        var html = EventDateChangeTemplate.Render(
            "Jazz Night",
            new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 25, 19, 0, 0, DateTimeKind.Utc),
            "reembolsos@ticketera.com");

        // The template should contain a human-readable old date
        Assert.Contains("15/10/2026", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_ContainsNewDate()
    {
        var html = EventDateChangeTemplate.Render(
            "Jazz Night",
            new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 25, 19, 0, 0, DateTimeKind.Utc),
            "reembolsos@ticketera.com");

        Assert.Contains("25/12/2026", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_ContainsRefundContactEmail()
    {
        var html = EventDateChangeTemplate.Render(
            "Rock Fest",
            new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc),
            "reembolsos@ticketera.com");

        Assert.Contains("reembolsos@ticketera.com", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TRIANGULATION: Different event name produces different output.
    /// The template must embed the event name, not just produce boilerplate.
    /// </summary>
    [Fact]
    public void Render_DifferentEventName_ProducesDifferentHtml()
    {
        var a = EventDateChangeTemplate.Render(
            "EventA",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 2, 2),
            "test@example.com");

        var b = EventDateChangeTemplate.Render(
            "EventB",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 2, 2),
            "test@example.com");

        Assert.Contains("EventA", a, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EventB", b, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// TRIANGULATION: Different refund contact must be reflected in output.
    /// </summary>
    [Fact]
    public void Render_DifferentContactEmail_ReflectedInOutput()
    {
        var a = EventDateChangeTemplate.Render(
            "Rock Fest",
            new DateTime(2026, 10, 15),
            new DateTime(2026, 11, 1),
            "contact-a@example.com");

        var b = EventDateChangeTemplate.Render(
            "Rock Fest",
            new DateTime(2026, 10, 15),
            new DateTime(2026, 11, 1),
            "contact-b@example.com");

        Assert.Contains("contact-a@example.com", a, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("contact-b@example.com", b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TRIANGULATION: The output must be valid HTML (DOCTYPE present).
    /// Matches the pattern of existing templates (TicketConfirmationTemplate, RefundNotificationTemplate).
    /// </summary>
    [Fact]
    public void Render_ProducesValidHtmlStructure()
    {
        var html = EventDateChangeTemplate.Render(
            "Rock Fest",
            new DateTime(2026, 10, 15),
            new DateTime(2026, 11, 1),
            "reembolsos@ticketera.com");

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<html lang=\"es\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</html>", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The notification must reassure buyers that their already-issued QR
    /// remains valid for the new date.
    /// </summary>
    [Fact]
    public void Render_ContainsQrValidityMessage()
    {
        var html = EventDateChangeTemplate.Render(
            "Rock Fest",
            new DateTime(2026, 10, 15),
            new DateTime(2026, 11, 1),
            "reembolsos@ticketera.com");

        Assert.Contains(
            "Tu QR ya emitido sigue siendo válido para la nueva fecha.",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The footer signature reads "— El equipo de TicketStart".
    /// </summary>
    [Fact]
    public void Render_FooterShowsTicketStartBrand()
    {
        var html = EventDateChangeTemplate.Render(
            "Rock Fest",
            new DateTime(2026, 10, 15),
            new DateTime(2026, 11, 1),
            "reembolsos@ticketera.com");

        Assert.Contains("El equipo de TicketStart", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("equipo de Ticketera", html, StringComparison.OrdinalIgnoreCase);
    }
}
