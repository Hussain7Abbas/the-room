using Godot;

namespace TheRoom.Config;

/// <summary>
/// A character's identity — CHARACTER-SPEC.md Part 4, "owned by the character's creator" column
/// (concept, art, animation, voice, personality). No gameplay numbers live here; the ability's
/// numbers are Tuning.AbilityNumbers, the melee numbers are shared by everyone (Tuning's Melee
/// groups). Silhouette/art fields are grey-box placeholders until a real art pass (Phase 5) —
/// SilhouetteColor is what actually renders today.
/// </summary>
[GlobalClass]
public partial class CharacterDef : Resource
{
    [Export] public string Id = ""; // lowercase, used as --character=&lt;id&gt; and folder name
    [Export] public string DisplayName = "";
    [Export] public string OwnerName = ""; // the real developer this is a caricature of — Pillar 3
    [Export] public string OneLinePersonality = "";
    [Export] public Color SilhouetteColor = Colors.White;

    [ExportGroup("Model & animation (any humanoid rig, see assets/animations/humanoid/README.md)")]
    /// <summary>Null means CharacterModel.DefaultModelPath (Zain).</summary>
    [Export] public PackedScene? Model;
    /// <summary>Null means the shared HumanoidAnimationSet.DefaultPath (or AnimationsPath).</summary>
    [Export] public TheRoom.Animation.HumanoidAnimationSet? Animations;
    /// <summary>A character's own animation set, by path, loaded only where models are shown
    /// (clients). The dedicated server never imports FBX files, so the registry mustn't load
    /// them at boot.</summary>
    [Export] public string AnimationsPath = "";
    /// <summary>Tint the model with SilhouetteColor. An untextured model takes the full colour; a
    /// textured one (like Zain) only a light wash, so skin doesn't turn blue. Turn it off to
    /// show the texture untouched.</summary>
    [Export] public bool TintModel = true;
    /// <summary>What the right hand holds. Null means CharacterModel.DefaultHeldPropPath (the knife).</summary>
    [Export] public PackedScene? HeldProp;

    [ExportGroup("Ability")]
    [Export] public AbilityDef? Ability;

    [ExportGroup("Passive quirk (small, always-on, flavour-first — CHARACTER-SPEC.md)")]
    [Export(PropertyHint.MultilineText)] public string PassiveDescription = "";
    [Export(PropertyHint.MultilineText)] public string PassiveWhyItFits = "";

    [ExportGroup("Voice lines (text placeholders — no audio assets yet, Phase 5)")]
    [Export] public string VoiceLineOnKill = "";
    [Export] public string VoiceLineOnDeath = "";
    [Export] public string VoiceLineOnDodge = "";
    [Export(PropertyHint.MultilineText)] public string DeathAnimationNotes = "";
}
