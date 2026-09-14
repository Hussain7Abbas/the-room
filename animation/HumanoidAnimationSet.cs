using Godot;

namespace TheRoom.Animation;

/// <summary>
/// The shared animation set every humanoid character plays. The clips are AnimationLibraries
/// imported from FBX with the humanoid retarget preset (see assets/animations/humanoid/README.md),
/// so their tracks target standard bone names (`%GeneralSkeleton:Hips`, `...:LeftUpperArm`) rather
/// than one model's own rig. Any character model imported with the same preset can play them.
///
/// A character uses DefaultPath unless its CharacterDef sets its own set, for example to
/// give one character a different run.
/// </summary>
[GlobalClass]
public partial class HumanoidAnimationSet : Resource
{
    public const string DefaultPath = "res://assets/animations/humanoid/humanoid_default.tres";

    /// <summary>Optional. Without one, idle holds the first frame of LightAttack, which is a
    /// standing stance in the Mixamo "Stabbing" clip, rather than a T-pose.</summary>
    [Export] public AnimationLibrary? Idle;
    [Export] public AnimationLibrary? Run;
    [Export] public AnimationLibrary? Jump;
    [Export] public AnimationLibrary? LightAttack;
    [Export] public AnimationLibrary? HeavyAttack;

    [ExportGroup("Attack clip playback")]
    /// <summary>Which slice of the attack clip to play, in seconds of the source clip. Mixamo
    /// clips often carry long lead-in or recovery that doesn't fit a 0.3s light attack.
    /// An end of 0 or less means "to the end of the clip".</summary>
    [Export] public float LightAttackClipStart;
    [Export] public float LightAttackClipEnd;
    [Export] public float HeavyAttackClipStart;
    [Export] public float HeavyAttackClipEnd;

    /// <summary>How long the attack animation lasts in game, in seconds. The slice above is sped up or
    /// slowed down to fit. Roughly windup + recovery from tuning.tres, so the swing lands with the hit.</summary>
    [Export] public float LightAttackDuration = 0.35f;
    [Export] public float HeavyAttackDuration = 0.9f;

    public static Godot.Animation? FirstClip(AnimationLibrary? library)
    {
        if (library is null)
            return null;
        var names = library.GetAnimationList();
        return names.Count > 0 ? library.GetAnimation(names[0]) : null;
    }
}
