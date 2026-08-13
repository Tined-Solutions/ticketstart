namespace TicketeraOnline.Api.Services;

/// <summary>
/// Typed options for the HideExpiredEvents runtime feature flag (EHE-009, ADR-4).
/// Bound to the <c>HideExpiredEvents</c> configuration section. The section itself
/// is REQUIRED at startup (fail-fast in Program.cs); within the section, Enabled
/// defaults to <c>true</c> via the property initializer, so an operator may omit
/// it and keep the feature active. Setting <c>Enabled=false</c> disables every
/// catalog filter and purchase guard (runtime rollback, no redeploy).
/// </summary>
public class HideExpiredEventsOptions
{
    public const string SectionName = "HideExpiredEvents";

    public bool Enabled { get; set; } = true;
}
