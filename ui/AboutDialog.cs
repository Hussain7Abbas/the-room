using Godot;

namespace TheRoom.UI;

/// <summary>About: what the game is, who made it, and a link to the team's GitHub.</summary>
public partial class AboutDialog : ModalDialog
{
    public const string OrgUrl = "https://github.com/Voidra-iq";

    public AboutDialog() : base("About", 520f)
    {
        Body.AddChild(UiTheme.Label("THE ROOM", "Title", fontSize: 44));

        var description = UiTheme.Label(
            "A fast, funny knife-fight deathmatch in one room. Everyone gets a knife and one ability. " +
            "Streaks put a bounty on your head, the Golden Knife makes every hit lethal, and every match " +
            "lands in a shared history. Each character is a caricature of the developer who built them.",
            fontSize: 15);
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Body.AddChild(description);

        var card = new PanelContainer { ThemeTypeVariation = "Card" };
        var team = new VBoxContainer();
        team.AddThemeConstantOverride("separation", 6);
        card.AddChild(team);
        team.AddChild(UiTheme.Label("Made by the Voidra team", "Heading", fontSize: 19));
        var link = new LinkButton { Text = "github.com/Voidra-iq", Uri = OrgUrl, FocusMode = FocusModeEnum.None };
        link.AddThemeFontSizeOverride("font_size", 15);
        team.AddChild(link);
        Body.AddChild(card);

        var credits = UiTheme.Label("Character model and animations from Mixamo. Built with Godot Engine.", "Muted", fontSize: 13);
        credits.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Body.AddChild(credits);
        Body.AddChild(UiTheme.Label("© 2026 Voidra Team. All rights reserved.", "Muted", fontSize: 13));
    }
}
