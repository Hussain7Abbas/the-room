using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("SeasonStats" in project.godot). Server-only persistence across matches AND across
/// server restarts (GDD §4 meta loop: "a persistent season leaderboard... cosmetic-only unlocks
/// tied to award milestones. No XP, no stat upgrades, no unlockable abilities."). This builds the
/// leaderboard data; cosmetic unlocks themselves are still blocked on Phase 5's roster existing
/// (nothing to unlock yet) — see plan/phase-6-meta-social.md.
///
/// Keyed by player display name, not peer id — peer ids aren't stable across sessions and there
/// is no account system (not building one here). Honest simplification: two different people
/// using the same display name share a season record.
///
/// Written to res://season_stats.json, which on the deployed server resolves to
/// /opt/the-room/app/season_stats.json — deploy/nginx/room-api.iscoded.com.conf serves this file
/// directly at /season, so the season leaderboard is visible outside the game (there's no
/// in-game lobby yet to show it in).
/// </summary>
public partial class SeasonStats : Node
{
    public static SeasonStats Instance { get; private set; } = null!;

    private static readonly string FilePath = ProjectSettings.GlobalizePath("res://season_stats.json");

    private class Record
    {
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int TotalScore { get; set; }
        public int BestScore { get; set; }
        public Dictionary<string, int> AwardCounts { get; set; } = new();
    }

    private Dictionary<string, Record> _records = new();
    private bool _isServer;

    public override void _Ready()
    {
        Instance = this;
        // Real dedicated server only, and only one NOT started by the lobby. Lobby rooms share a
        // working directory, so several of them writing this one JSON file would overwrite each
        // other; their results go to the lobby's match history instead (core/RoomReporter.cs).
        _isServer = Net.Instance.IsServer && Net.Instance.LobbyUrl is null;
        if (_isServer)
            Load();
    }

    /// <summary>Server-only. Called from MatchServer.EndMatch with the same standings/awards
    /// the results screen shows. `standings` must already be sorted descending by score.</summary>
    public void RecordMatch(IReadOnlyList<(string name, int score)> standings, IReadOnlyList<string> awardLines)
    {
        if (!_isServer || standings.Count == 0)
            return;

        var winnerScore = standings[0].score;
        foreach (var (name, score) in standings)
        {
            if (!_records.TryGetValue(name, out var record))
            {
                record = new Record();
                _records[name] = record;
            }

            record.MatchesPlayed++;
            record.TotalScore += score;
            record.BestScore = Math.Max(record.BestScore, score);
            if (score == winnerScore)
                record.Wins++;
        }

        // Award lines look like "TITLE: Name (N stat)" — see MatchServer.ComputeAwards.
        foreach (var line in awardLines)
        {
            var titleSep = line.IndexOf(':');
            if (titleSep < 0)
                continue;

            var title = line[..titleSep].Trim();
            var rest = line[(titleSep + 1)..].Trim();
            var nameEnd = rest.IndexOf('(');
            if (nameEnd < 0)
                continue;

            var winnerName = rest[..nameEnd].Trim();
            if (!_records.TryGetValue(winnerName, out var record))
                continue;

            record.AwardCounts[title] = record.AwardCounts.GetValueOrDefault(title) + 1;
        }

        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                _records = JsonSerializer.Deserialize<Dictionary<string, Record>>(File.ReadAllText(FilePath)) ?? new();
                GD.Print($"[SeasonStats] Loaded {_records.Count} player record(s) from {FilePath}.");
            }
        }
        catch (Exception e)
        {
            GD.PushError($"[SeasonStats] Failed to load {FilePath}: {e.Message}");
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            GD.PushError($"[SeasonStats] Failed to save {FilePath}: {e.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Results webhook (GDD §4: "a results webhook that posts the match summary into the team
    // chat"). OFF by default — no URL is ever committed to this repo, and none is guessed or
    // fabricated here. To enable on the deployed server: set the RESULTS_WEBHOOK_URL environment
    // variable in the systemd unit to a real Discord/Slack/Teams incoming-webhook URL (see
    // plan/phase-6-meta-social.md for the exact steps). Payload uses a plain {"text": "..."}
    // body, which Slack/Mattermost-style incoming webhooks accept directly; Discord's webhook
    // endpoint also accepts a "content" field instead — may need a one-line tweak below
    // depending on which platform the team actually uses.
    // ------------------------------------------------------------------

    private string? _webhookUrl;
    private bool _webhookUrlChecked;
    private HttpRequest? _webhookRequest;

    public void PostResultsWebhook(string mvpText, IReadOnlyList<(string name, int score)> standings, IReadOnlyList<string> awardLines)
    {
        if (!_isServer)
            return;

        if (!_webhookUrlChecked)
        {
            _webhookUrl = System.Environment.GetEnvironmentVariable("RESULTS_WEBHOOK_URL");
            _webhookUrlChecked = true;
        }

        if (string.IsNullOrWhiteSpace(_webhookUrl))
            return; // not configured — expected on most deployments, silent no-op

        _webhookRequest ??= CreateWebhookRequestNode();

        var lines = new List<string> { $"**Match over** — {mvpText}" };
        foreach (var (name, score) in standings)
            lines.Add($"{name}: {score} pts");
        lines.AddRange(awardLines);

        var payload = JsonSerializer.Serialize(new { text = string.Join("\n", lines) });
        var headers = new[] { "Content-Type: application/json" };
        var err = _webhookRequest.Request(_webhookUrl, headers, Godot.HttpClient.Method.Post, payload);
        if (err != Error.Ok)
            GD.PushError($"[SeasonStats] Webhook request failed to start: {err}");
    }

    private HttpRequest CreateWebhookRequestNode()
    {
        var node = new HttpRequest();
        AddChild(node);
        node.RequestCompleted += (long result, long responseCode, string[] responseHeaders, byte[] body) =>
        {
            if (responseCode is < 200 or >= 300)
                GD.PushError($"[SeasonStats] Webhook responded with status {responseCode}.");
        };
        return node;
    }
}
