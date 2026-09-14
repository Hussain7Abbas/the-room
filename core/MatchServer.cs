using System.Collections.Generic;
using Godot;
using TheRoom.Entities;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("MatchServer" in project.godot). Owns the match loop (GDD §5.6/§5.5/§5.4, Pillar 4):
/// score/timer, Last Call, results + auto-rematch, Bounty, and the Golden Knife. Authoritative
/// on the server (and the offline-solo instance, which is its own authority same as everywhere
/// else in this codebase); every other peer just reacts to broadcasts from here, same
/// CallLocal=true pattern as Player.BroadcastKill.
///
/// Deliberately NOT built here (see plan/phase-4-match-shape.md): real shutter geometry for
/// Last Call (grey-box stand-in is a screen-tint announcement instead — level-design work, not
/// a systems gap), full map modularity / Duel-Pit-vs-Chaos map sections (one static room exists;
/// --config only switches the score target), a structured telemetry log (console prints only).
/// </summary>
public partial class MatchServer : Node
{
    public static MatchServer Instance { get; private set; } = null!;

    private enum MatchState { InProgress, LastCall, Ended }
    private MatchState _state = MatchState.InProgress;
    private double _matchElapsed;
    private double _resultsTimeRemaining;
    private int _scoreTarget;
    private bool _isAuthoritative; // this peer actually runs match logic (server, or offline-solo)

    private readonly Dictionary<long, int> _score = new();
    private readonly Dictionary<long, int> _bounty = new();

    public bool IsLastCall => _state == MatchState.LastCall;
    public bool IsResults => _state == MatchState.Ended;
    public int GetScore(long peerId) => _score.GetValueOrDefault(peerId);
    public int GetBounty(long peerId) => _bounty.GetValueOrDefault(peerId);
    public float MatchTimeRemaining => Mathf.Max(0f, TuningService.Instance.MatchTimeLimitSeconds - (float)_matchElapsed);
    public int ScoreTarget => _scoreTarget;

    // --- Golden Knife (GDD §5.4) ---
    private enum KnifeState { Respawning, Available, Held }
    private KnifeState _knifeState = KnifeState.Respawning;
    private double _knifeTimer;
    private long _knifeHolderId = -1;
    private static readonly Vector3 KnifeSpawnPosition = new(0, 1.5f, 0);
    private CsgBox3D? _knifeVisual;

    public bool IsGoldenKnifeHolder(long peerId) => _knifeState == KnifeState.Held && _knifeHolderId == peerId;

    public override void _Ready()
    {
        Instance = this;
        _isAuthoritative = Net.Instance.IsServer || Net.Instance.IsOffline;
        _scoreTarget = Net.Instance.IsChaosConfig ? TuningService.Instance.ScoreTargetChaos : TuningService.Instance.ScoreTargetDuelPit;
        _knifeTimer = TuningService.Instance.GoldenKnifeFirstSpawn;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isAuthoritative)
            return;

        TickMatch(delta);
        TickGoldenKnife(delta);
    }

    private void TickMatch(double delta)
    {
        var tuning = TuningService.Instance;

        switch (_state)
        {
            case MatchState.InProgress:
            case MatchState.LastCall:
                _matchElapsed += delta;
                var timeLeft = tuning.MatchTimeLimitSeconds - _matchElapsed;
                var leaderScore = 0;
                foreach (var s in _score.Values)
                    if (s > leaderScore) leaderScore = s;

                if (_state == MatchState.InProgress &&
                    (leaderScore >= _scoreTarget * tuning.LastCallThresholdPercent || timeLeft <= tuning.LastCallTimeRemainingSeconds))
                {
                    EnterLastCall();
                }

                if (leaderScore >= _scoreTarget || timeLeft <= 0)
                {
                    EndMatch();
                }
                break;

            case MatchState.Ended:
                _resultsTimeRemaining -= delta;
                if (_resultsTimeRemaining <= 0)
                    ResetMatch();
                break;
        }
    }

    private void EnterLastCall()
    {
        _state = MatchState.LastCall;
        GD.Print("[Match] Last Call.");
        Rpc(nameof(BroadcastAnnouncement), "LAST CALL");
    }

    private void EndMatch()
    {
        _state = MatchState.Ended;
        _resultsTimeRemaining = TuningService.Instance.ResultsScreenDurationSeconds;

        long mvpId = -1;
        var mvpScore = -1;
        foreach (var (id, s) in _score)
        {
            if (s <= mvpScore) continue;
            mvpScore = s;
            mvpId = id;
        }

        var mvpName = mvpId >= 0 ? Main.GetPlayerName(mvpId) : "Nobody";
        GD.Print($"[Match] Ended. MVP: {mvpName} ({Mathf.Max(0, mvpScore)} pts).");
        Rpc(nameof(BroadcastAnnouncement), $"MATCH OVER — MVP: {mvpName} ({Mathf.Max(0, mvpScore)} pts)");
    }

    private void ResetMatch()
    {
        GD.Print("[Match] New match starting.");
        _state = MatchState.InProgress;
        _matchElapsed = 0;
        _score.Clear();
        _bounty.Clear();

        _knifeState = KnifeState.Respawning;
        _knifeTimer = TuningService.Instance.GoldenKnifeFirstSpawn;
        _knifeHolderId = -1;

        foreach (var player in CombatServer.Instance.AllPlayers())
            player.ServerMatchReset();

        Rpc(nameof(BroadcastAnnouncement), "NEW MATCH");
    }

    /// <summary>Server-only (or offline-solo). Called from Player.ServerApplyDamage's lethal
    /// branch — updates score, bounty, and Golden Knife holder state for one kill.</summary>
    public void ServerRegisterKill(long attackerId, long victimId)
    {
        if (!_isAuthoritative || _state == MatchState.Ended)
            return; // no scoring during the results screen

        var tuning = TuningService.Instance;
        var victimBounty = _bounty.GetValueOrDefault(victimId);
        var points = 1 + victimBounty;

        if (IsGoldenKnifeHolder(attackerId))
            points = Mathf.RoundToInt(points * tuning.GoldenKnifeScoreMultiplier);

        _score[attackerId] = _score.GetValueOrDefault(attackerId) + points;
        _bounty[attackerId] = _bounty.GetValueOrDefault(attackerId) + 1;
        _bounty[victimId] = 0;

        var newBounty = _bounty[attackerId];
        if (newBounty == tuning.BountyAnnounceOnATear)
            Rpc(nameof(BroadcastAnnouncement), $"{Main.GetPlayerName(attackerId)} IS ON A TEAR");
        else if (newBounty == tuning.BountyAnnounceSecond || newBounty == tuning.BountyAnnounceThird)
            Rpc(nameof(BroadcastAnnouncement), $"{Main.GetPlayerName(attackerId)} IS UNSTOPPABLE ({newBounty})");

        if (victimBounty > 0)
            Rpc(nameof(BroadcastAnnouncement), $"{Main.GetPlayerName(attackerId)} COLLECTED {Main.GetPlayerName(victimId)}'S BOUNTY (+{victimBounty})");

        if (IsGoldenKnifeHolder(victimId))
            LoseGoldenKnife();

        Rpc(nameof(BroadcastScore), attackerId, _score[attackerId]);
    }

    /// <summary>Server-only. Player.ServerApplyDamage picks between Tuning.RespawnTime and
    /// RespawnTimeLastCall based on this.</summary>
    public float CurrentRespawnTime => IsLastCall ? TuningService.Instance.RespawnTimeLastCall : TuningService.Instance.RespawnTime;

    // ------------------------------------------------------------------
    // Golden Knife
    // ------------------------------------------------------------------

    private void TickGoldenKnife(double delta)
    {
        switch (_knifeState)
        {
            case KnifeState.Respawning:
                _knifeTimer -= delta;
                if (_knifeTimer <= 0)
                {
                    _knifeState = KnifeState.Available;
                    Rpc(nameof(BroadcastAnnouncement), "THE GOLDEN KNIFE HAS APPEARED");
                }
                break;

            case KnifeState.Available:
                foreach (var player in CombatServer.Instance.AllPlayers())
                {
                    if (player.IsDead)
                        continue;
                    if (player.GlobalPosition.DistanceTo(KnifeSpawnPosition) > TuningService.Instance.GoldenKnifePickupRadius)
                        continue;

                    _knifeState = KnifeState.Held;
                    _knifeHolderId = player.PeerId;
                    _knifeTimer = TuningService.Instance.GoldenKnifeDuration;
                    Rpc(nameof(BroadcastAnnouncement), $"{Main.GetPlayerName(player.PeerId)} TOOK THE GOLDEN KNIFE");
                    break;
                }
                break;

            case KnifeState.Held:
                _knifeTimer -= delta;
                if (_knifeTimer <= 0)
                    LoseGoldenKnife();
                break;
        }
    }

    private void LoseGoldenKnife()
    {
        _knifeState = KnifeState.Respawning;
        _knifeHolderId = -1;
        _knifeTimer = TuningService.Instance.GoldenKnifeRespawnDelay;
        Rpc(nameof(BroadcastAnnouncement), "THE GOLDEN KNIFE WAS LOST");
    }

    /// <summary>Cosmetic-only pickup marker (grey-box: a glowing gold box) — spawned/removed on
    /// every peer independently in reaction to the announcement broadcast, same pattern as
    /// Player.SpawnDeathEffect. No collision, purely visual; the actual pickup check above is
    /// a plain distance check run only where _isAuthoritative is true.</summary>
    private void SetKnifeVisual(bool visible)
    {
        if (visible)
        {
            if (_knifeVisual is not null && IsInstanceValid(_knifeVisual))
                return;

            var tree = (SceneTree)Engine.GetMainLoop();
            var root = tree.CurrentScene;
            if (root is null)
                return;

            var box = new CsgBox3D { Size = new Vector3(0.3f, 0.3f, 1.0f), Position = KnifeSpawnPosition };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.85f, 0.1f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.85f, 0.1f),
            };
            box.MaterialOverride = mat;
            root.AddChild(box);
            _knifeVisual = box;
        }
        else
        {
            if (_knifeVisual is not null && IsInstanceValid(_knifeVisual))
                _knifeVisual.QueueFree();
            _knifeVisual = null;
        }
    }

    // ------------------------------------------------------------------
    // Broadcasts — autoloads have the same stable NodePath on every peer, so RPCs work the same
    // way as Player's per-node broadcasts (BroadcastKill), just without needing a Player instance.
    // ------------------------------------------------------------------

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BroadcastAnnouncement(string text)
    {
        if (!_isAuthoritative && Multiplayer.GetRemoteSenderId() != 1)
            return;

        GD.Print($"[Match] {text}");

        if (text.Contains("GOLDEN KNIFE HAS APPEARED")) SetKnifeVisual(true);
        else if (text.Contains("TOOK THE GOLDEN KNIFE") || text.Contains("GOLDEN KNIFE WAS LOST")) SetKnifeVisual(false);

        Events.Instance.EmitSignal(Events.SignalName.MatchAnnouncement, text);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void BroadcastScore(long peerId, int newScore)
    {
        if (!_isAuthoritative && Multiplayer.GetRemoteSenderId() != 1)
            return;

        _score[peerId] = newScore;
    }
}
