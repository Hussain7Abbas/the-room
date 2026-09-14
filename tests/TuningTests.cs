using Chickensoft.GoDotTest;
using Godot;
using Shouldly;
using TheRoom.Config;

namespace TheRoom.Tests;

/// <summary>
/// Deferred since Phase 0 (see plan/phase-0-foundation.md), finally set up here —
/// Chickensoft.GoDotTest (NuGet-only, no editor addon needed, unlike gdUnit4). Run with
/// `make test` or `godot --headless --path . -- --run-tests` directly.
/// </summary>
public class TuningTests : TestClass
{
    public TuningTests(Node testScene) : base(testScene) { }

    [Test]
    public void GetAbilityNumberReturnsFallbackWhenKeyMissing()
    {
        var tuning = new Tuning();
        tuning.GetAbilityNumber("nonexistent-ability", "some-param", 42f).ShouldBe(42f);
    }

    [Test]
    public void GetAbilityNumberReturnsStoredValueWhenPresent()
    {
        var tuning = new Tuning();
        tuning.AbilityNumbers["dropkick.range"] = 6f;
        tuning.GetAbilityNumber("dropkick", "range", -1f).ShouldBe(6f);
    }
}
