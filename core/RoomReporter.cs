using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("RoomReporter"). Only active in a game server the lobby started (services/lobby
/// passes --lobby-url/--room-code/--room-token). Sends a heartbeat every few seconds so the room
/// list shows live player counts and the lobby can close empty rooms, and posts every finished
/// match to the match history. Talks to the lobby over loopback; the token proves it's the
/// room the lobby started.
/// </summary>
public partial class RoomReporter : Node
{
    public sealed record PlayerResult(string Name, string Character, int Score, int Kills, int Deaths);

    private const double HeartbeatIntervalSeconds = 5.0;
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RoomReporter Instance { get; private set; } = null!;

    private double _heartbeatIn;
    private bool _heartbeatInFlight;

    private static bool Enabled =>
        Net.Instance.IsServer && Net.Instance.LobbyUrl is not null && Net.Instance.RoomCode is not null && Net.Instance.RoomToken is not null;

    private static string RoomUrl => $"{Net.Instance.LobbyUrl!.TrimEnd('/')}/internal/rooms/{Uri.EscapeDataString(Net.Instance.RoomCode!)}";

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        if (!Enabled || _heartbeatInFlight)
            return;

        _heartbeatIn -= delta;
        if (_heartbeatIn > 0)
            return;
        _heartbeatIn = HeartbeatIntervalSeconds;
        _ = SendHeartbeat();
    }

    private async Task SendHeartbeat()
    {
        _heartbeatInFlight = true;
        try
        {
            var players = Multiplayer.GetPeers().Length;
            var state = players == 0 ? "waiting" : MatchServer.Instance.StateName;
            using var response = await Http.PostAsJsonAsync($"{RoomUrl}/heartbeat",
                new { token = Net.Instance.RoomToken, players, state }, Json);
            if (!response.IsSuccessStatusCode)
                GD.PushWarning($"[Lobby] Heartbeat rejected: HTTP {(int)response.StatusCode}.");
        }
        catch (Exception e)
        {
            GD.PushWarning($"[Lobby] Heartbeat failed: {e.Message}");
        }
        finally
        {
            _heartbeatInFlight = false;
        }
    }

    /// <summary>Called by MatchServer.EndMatch. If the lobby can't be reached it retries once after
    /// a short delay, so one dropped request doesn't lose the match from the history.</summary>
    public void ReportMatch(double durationSeconds, IReadOnlyList<PlayerResult> players, IReadOnlyList<string> awards)
    {
        if (!Enabled || players.Count == 0)
            return;
        _ = PostMatch(new { token = Net.Instance.RoomToken, durationSeconds, players, awards });
    }

    private static async Task PostMatch(object payload)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var response = await Http.PostAsJsonAsync($"{RoomUrl}/matches", payload, Json);
                if (response.IsSuccessStatusCode)
                {
                    GD.Print("[Lobby] Match recorded in history.");
                    return;
                }
                GD.PushWarning($"[Lobby] Match report rejected: HTTP {(int)response.StatusCode}.");
                return; // a 4xx won't succeed on retry
            }
            catch (Exception e)
            {
                GD.PushWarning($"[Lobby] Match report failed (attempt {attempt}): {e.Message}");
                await Task.Delay(2000);
            }
        }
    }
}
