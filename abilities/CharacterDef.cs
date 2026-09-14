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
    /// <summary>Null means the shared HumanoidAnimationSet.DefaultPath.</summary>
    [Export] public TheRoom.Animation.HumanoidAnimationSet? Animations;
    /// <summary>Tint the model with SilhouetteColor. Right for untextured models like Zain; turn
    /// it off for a textured model, or the colour multiplies over the texture.</summary>
    [Export] public bool TintModel = true;

    [ExportGroup("Ability")]
    [Export] public AbilityDef? Ability;

    [ExportGroup("Passive quirk (small, always-on, flavour-first — CHARACTER-SPEC.md)")]
    [Export(PropertyHint.MultilineText)] public string PassiveDescription = "";
    [Export(PropertyHint.MultilineText)] public string PassiveWhyItFits = "";

    [ExportGroup("Voice lines (text placeholders — no audio assets yet, Phase 5)")]
    [Export] public string VoiceLineOnKill = "";
    [Export] public string VoiceLineOnDeath = "";
    [Export] public string VoiceLineOnParry = "";
    [Export(PropertyHint.MultilineText)] public string DeathAnimationNotes = "";
}
