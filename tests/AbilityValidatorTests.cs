using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;
using TheRoom.Config;

namespace TheRoom.Tests;

/// <summary>
/// Regression test for the mechanical half of CHARACTER-SPEC.md's review gate — catches a
/// broken ability (missing tell, out-of-range cooldown, slot collision, ...) locally before it
/// ever reaches a running server. AbilityValidator.RunAndPrint() also runs automatically on
/// every server boot (core/Net.cs); this just makes it a normal `make test` failure too.
/// </summary>
public class AbilityValidatorTests : TestClass
{
    public AbilityValidatorTests(Node testScene) : base(testScene) { }

    [Test]
    public void CurrentRosterHasNoGrammarViolations()
    {
        var issues = AbilityValidator.ValidateAll();
        issues.ShouldBeEmpty(string.Join("; ", issues.Select(i => $"{i.CharacterId}: {i.Message}")));
    }
}
