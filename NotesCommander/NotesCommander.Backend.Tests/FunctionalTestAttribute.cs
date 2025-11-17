using Xunit;
using Xunit.Sdk;

namespace NotesCommander.Backend.Tests;

/// <summary>
/// Marks a test as a functional test that requires external dependencies (Docker, network, etc.)
/// These tests are excluded from regular unit test runs.
/// To run functional tests, use: dotnet test --filter Category=Functional
/// </summary>
[XunitTestCaseDiscoverer("Xunit.Sdk.FactDiscoverer", "xunit.execution.{Platform}")]
[TraitAttribute("Category", "Functional")]
public sealed class FunctionalTestAttribute : FactAttribute
{
}

