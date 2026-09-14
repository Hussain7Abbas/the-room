using System.Collections.Generic;
using System.Linq;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;
using TheRoom.Animation;
using TheRoom.Config;

namespace TheRoom.Tests;

/// <summary>
/// The contract that makes animations pluggable: every clip in the shared set targets bones by
/// their standard humanoid names, and every character model has those bones. A new model
/// imported without the humanoid retarget preset (assets/animations/humanoid/README.md) fails
/// here instead of T-posing in game.
/// </summary>
public class CharacterAnimationTests : TestClass
{
    public CharacterAnimationTests(Node testScene) : base(testScene) { }

    private static HumanoidAnimationSet DefaultSet() =>
        GD.Load<HumanoidAnimationSet>(HumanoidAnimationSet.DefaultPath);

    private static HumanoidAnimationSet ZainSet() =>
        GD.Load<HumanoidAnimationSet>(CharacterRegistry.GetOrDefault("zain").AnimationsPath);

    [Test]
    public void ZainActsOutHisDropKick()
    {
        CharacterRegistry.GetOrDefault("zain").Ability!.Id.ShouldBe("dropkick");
        var model = CharacterModel.Create(GD.Load<PackedScene>(CharacterModel.DefaultModelPath), ZainSet(), 1.8f);
        try
        {
            model.GetClip(CharacterModel.Clip.Ability).ShouldNotBeNull();
        }
        finally
        {
            model.Free();
        }
    }

    private static IEnumerable<(string Name, PackedScene Scene)> AllModels()
    {
        yield return ("default", GD.Load<PackedScene>(CharacterModel.DefaultModelPath));
        foreach (var def in CharacterRegistry.All.Values.Where(d => d.Model is not null))
            yield return (def.Id, def.Model!);
    }

    [Test]
    public void EveryClipTargetsBonesPresentOnEveryModel()
    {
        var set = DefaultSet();
        var zain = ZainSet();
        var clips = new[] { set.Idle, set.Run, set.Jump, set.LightAttack, set.HeavyAttack, set.Dodge, set.Death, zain.Ability }
            .Select(HumanoidAnimationSet.FirstClip)
            .Where(c => c is not null)
            .ToList();
        clips.Count.ShouldBeGreaterThanOrEqualTo(3); // run, jump, attack at minimum

        var missing = new List<string>();
        foreach (var (name, scene) in AllModels())
        {
            var root = scene.Instantiate<Node3D>();
            var skeleton = root.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().Single();
            foreach (var clip in clips)
            {
                for (var t = 0; t < clip!.GetTrackCount(); t++)
                {
                    var path = clip.TrackGetPath(t);
                    var bone = path.GetConcatenatedSubNames();
                    if (path.GetName(path.GetNameCount() - 1) != "%" + skeleton.Name || skeleton.FindBone(bone) < 0)
                        missing.Add($"{name}: {path}");
                }
            }
            root.Free();
        }

        missing.Distinct().ShouldBeEmpty();
    }

    [Test]
    public void HeldPropAttachesToTheRightHand()
    {
        var model = CharacterModel.Create(GD.Load<PackedScene>(CharacterModel.DefaultModelPath), DefaultSet(), 1.8f,
            GD.Load<PackedScene>(CharacterModel.DefaultHeldPropPath));
        try
        {
            model.HeldProp.ShouldNotBeNull();
            var attachment = model.HeldProp!.GetParent().ShouldBeOfType<BoneAttachment3D>();
            attachment.BoneName.ShouldBe("RightHand");
            attachment.GetParent().ShouldBeOfType<Skeleton3D>();
        }
        finally
        {
            model.Free();
        }
    }

    [Test]
    public void BuiltModelHasEveryClipAndNoRootMotion()
    {
        var model = CharacterModel.Create(GD.Load<PackedScene>(CharacterModel.DefaultModelPath), DefaultSet(), 1.8f);
        try
        {
            // Ability is character-specific: the shared set has none, Zain's has the drop kick.
            foreach (var clip in System.Enum.GetValues<CharacterModel.Clip>())
            {
                if (clip == CharacterModel.Clip.Ability)
                    model.GetClip(clip).ShouldBeNull();
                else
                    model.GetClip(clip).ShouldNotBeNull($"clip {clip}");
            }

            var run = model.GetClip(CharacterModel.Clip.Run)!;
            run.LoopMode.ShouldBe(Godot.Animation.LoopModeEnum.Linear);
            for (var t = 0; t < run.GetTrackCount(); t++)
            {
                if (run.TrackGetType(t) != Godot.Animation.TrackType.Position3D)
                    continue;
                var first = run.TrackGetKeyValue(t, 0).AsVector3();
                var last = run.TrackGetKeyValue(t, run.TrackGetKeyCount(t) - 1).AsVector3();
                new Vector2(last.X - first.X, last.Z - first.Z).Length().ShouldBeLessThan(0.001f);
            }
        }
        finally
        {
            model.Free();
        }
    }
}
