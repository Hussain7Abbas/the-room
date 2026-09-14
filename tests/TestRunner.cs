using System.Reflection;
using Chickensoft.GoDotTest;
using Godot;

namespace TheRoom.Tests;

/// <summary>
/// Entry point for `make test` / `godot --headless --path . tests/TestRunner.tscn -- --run-tests --quit-on-finish`.
/// Deliberately a separate scene from core/Main.tscn (passed on the command line, overriding the
/// project's normal main scene for this one run) rather than baked into the real game's boot —
/// autoloads (TuningService, etc.) still initialize normally either way, since those are
/// project-wide, not scene-specific.
/// </summary>
public partial class TestRunner : Node
{
    public override void _Ready() => _ = CallDeferred(nameof(Go));

    private void Go()
    {
        // OS.GetCmdlineArgs() does NOT include args after a literal "--" (the same gotcha
        // documented in core/Net.cs, rediscovered here) — GoDotTest's own flags (--run-tests,
        // --quit-on-finish, ...) are always passed after "--", so this must be GetCmdlineUserArgs().
        var testEnv = TestEnvironment.From(OS.GetCmdlineUserArgs());
        _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, testEnv);
    }
}
