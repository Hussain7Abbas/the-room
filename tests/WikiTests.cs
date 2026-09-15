using System.Linq;
using System.Text.RegularExpressions;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;
using TheRoom.UI;

namespace TheRoom.Tests;

/// <summary>The player wiki (wiki/, ui/Wiki.cs): no broken links, anchors or images, every page
/// reachable from the home page, and search that finds things.</summary>
public class WikiTests : TestClass
{
    public WikiTests(Node testScene) : base(testScene) { }

    [Test]
    public void PagesLoadAndHomeComesFirst()
    {
        Wiki.Reload();
        Wiki.Pages.Count.ShouldBeGreaterThan(5);
        Wiki.Pages[0].Id.ShouldBe(Wiki.HomeId);
        Wiki.Pages.ShouldNotContain(p => p.Id == "CLAUDE"); // the standards file isn't a page
    }

    [Test]
    public void HomeLinksEveryPage()
    {
        var home = Wiki.Get(Wiki.HomeId)!;
        foreach (var page in Wiki.Pages.Where(p => p.Id != Wiki.HomeId))
            home.Markdown.ShouldContain($"({page.Id}.md", customMessage: $"intro.md doesn't link {page.Id}.md");
    }

    [Test]
    public void EveryLinkAnchorAndImageResolves()
    {
        foreach (var page in Wiki.Pages)
        {
            foreach (Match m in Regex.Matches(page.Markdown, @"(!?)\[([^\]]*)\]\(([^)\s]+)\)"))
            {
                var target = m.Groups[3].Value;
                if (m.Groups[1].Value == "!")
                {
                    ResourceLoader.Exists(Wiki.Dir + target).ShouldBeTrue($"{page.Id}: missing image {target}");
                    continue;
                }
                var (id, anchor) = Wiki.ParseTarget(target, page.Id);
                if (id is null)
                    continue; // external
                var linked = Wiki.Get(id);
                linked.ShouldNotBeNull($"{page.Id}: link to missing page {target}");
                if (anchor.Length > 0)
                    linked!.Sections.ShouldContain(s => s.Anchor == anchor, $"{page.Id}: link to missing section {target}");
            }
        }
    }

    [Test]
    public void SlugsMatchGitHub()
    {
        Wiki.Slug("The roll (dodge)").ShouldBe("the-roll-dodge");
        Wiki.Slug("Sprint, jump and stamina").ShouldBe("sprint-jump-and-stamina");
        Wiki.Slug("Zain: Drop Kick").ShouldBe("zain-drop-kick");
    }

    [Test]
    public void SearchFindsSectionsWithEveryWord()
    {
        var hits = Wiki.Search("golden knife");
        hits.ShouldNotBeEmpty();
        hits[0].Page.Id.ShouldBe("match-rules");
        Wiki.Search("zzzqqq").ShouldBeEmpty();
    }

    [Test]
    public void BbcodeRendersLinksAndEscapesBrackets()
    {
        var bbcode = Wiki.ToBbcode(Wiki.Get("combat")!, 600);
        bbcode.ShouldContain("[url=match-rules#the-golden-knife]");
        bbcode.ShouldContain("[table=");
        bbcode.ShouldNotContain("](");
    }
}
