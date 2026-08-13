using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Serializes test classes that mutate PROCESS-GLOBAL environment variables
/// (configuration overrides applied by WebApplicationFactory factories and
/// per-test SetEnv helpers). Environment variables are a process-wide resource:
/// classes in this collection must never boot hosts concurrently, or one class's
/// values can clobber another's mid-boot. Exposed deterministically by the
/// HideExpiredEvents fail-fast startup guard (ConfigValidationTests vs
/// ScaffoldRemovalTests race); the collection makes that impossible.
/// </summary>
[CollectionDefinition("EnvConfigTests", DisableParallelization = true)]
public class EnvConfigTestCollection
{
}
