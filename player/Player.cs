using System.Collections.Generic;
using Godot;
using TheRoom.Core;

namespace TheRoom.Entities;

/// <summary>
/// Player controller with three distinct simulation roles depending on who's looking at it
/// (see plan/phase-1-network-spike.md):
///   - Server: always runs the ONE authoritative physics simulation, driven by the latest
///     input received from the owning client.
///   - Owning client: predicts movement locally from real input (zero perceived latency),
///     sends that input to the server, and reconciles toward the server's periodic
///     corrections when they disagree.
///   - Every other client (watching a peer that isn't them and isn't the server): runs no
///     physics at all — just interpolates visually between buffered server snapshots,
///     delayed by Tuning.InterpolationDelaySeconds.
/// Offline mode (no MultiplayerPeer) skips all of the above and behaves like Phase 0: predict
/// only, no server, no reconciliation.
///
/// Also carries the Phase-1 network-spike-only "stab" verb: a single fixed-damage melee check
/// with server-side rewind lag compensation, entirely temporary — Phase 2 replaces it with the
/// real light/heavy/parry/dash system.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float MoveSpeed = 6.0f;
    [Export] public float MouseSensitivity = 0.0035f;
    [Export] public float MinPitchDegrees = -60f;
    [Export] public float MaxPitchDegrees = 70f;

    [Export] public NodePath SpringArmPath = "CameraPivot/SpringArm3D";
    [Export] public Label3D? NameLabel;

    private SpringArm3D _springArm = null!;
    private float _pitchRadians;

    // --- role, decided once in _Ready ---
    private bool _isServer;
    private bool _isOffline;
    private bool _isOwner;      // this machine predicts/controls this node (owning client, or offline local player)
    private bool _isRemoteView; // another client watching a peer that is neither them nor the server
    private bool _isBot;
    private long _peerId;

    // --- client-side prediction (owning client, networked only) ---
    private readonly List<PredictedState> _predictedHistory = new();
    private readonly struct PredictedState
    {
        public readonly ulong Tick;
        public readonly Vector3 Position;
        public PredictedState(ulong tick, Vector3 position) { Tick = tick; Position = position; }
    }

    // --- server-authoritative input, latest received from the owning client (server only) ---
    private Vector2 _serverPendingInput;
    private float _serverPendingYaw;

    // --- remote entity interpolation (remote-view clients only) ---
    private readonly List<RemoteSnapshot> _remoteSnapshots = new();
    private readonly struct RemoteSnapshot
    {
        public readonly double ReceivedAt;
        public readonly Vector3 Position;
        public readonly float YawRadians;
        public RemoteSnapshot(double receivedAt, Vector3 position, float yaw) { ReceivedAt = receivedAt; Position = position; YawRadians = yaw; }
    }

    // --- network-spike stab verb (all roles keep their own cooldown; server's is authoritative) ---
    private float _stabCooldownRemaining;
    private float _health;

    // --- bot AI (owning client with --bot; see core/Net.cs) ---
    private Vector2 _botMoveInput;
    private Vector3 _botTarget;
    private double _botRetargetIn;
    private double _botStabIn;

    // --- debug overlay stats (owning client only, see core/DebugOverlay.cs) ---
    public int PredictedStabCount { get; private set; }
    public int ConfirmedStabCount { get; private set; }
    public float LastRewindMs { get; private set; }
    public bool IsLocallyControlled => _isOwner;
    public bool IsBotControlled => _isBot;
    public string RoleLabel => _isServer ? "Server" : _isOffline ? "Offline" : _isBot ? "Bot" : _isOwner ? "Client (owner)" : "Remote view";

    public override void _Ready()
    {
        _springArm = GetNode<SpringArm3D>(SpringArmPath);
        _health = TuningService.Instance.MaxHealth;
        _peerId = long.TryParse(Name, out var parsedId) ? parsedId : GetMultiplayerAuthority();

        // MultiplayerSpawner replicates node creation, NOT the multiplayer-authority flag — every
        // peer's own local copy of a spawned node defaults to authority 1 (the server) until it
        // sets this itself. The node's Name is the owning peer id (see Main.SpawnPlayer), so every
        // peer — including the owner — can derive the correct authority here identically.
        SetMultiplayerAuthority((int)_peerId);

        _isServer = Net.Instance.IsServer;
        _isOffline = Net.Instance.IsOffline;
        _isOwner = IsMultiplayerAuthority();
        _isRemoteView = !_isServer && !_isOwner;
        _isBot = _isOwner && !_isServer && !_isOffline && Net.Instance.IsBot;

        if (_isServer)
            CombatServer.Instance.RegisterPlayer(_peerId, this);

        if (_isOwner)
        {
            var camera = _springArm.GetNodeOrNull<Camera3D>("Camera3D");
            camera?.MakeCurrent();
            PickNewBotTarget();

            if (!_isBot)
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _ExitTree()
    {
        if (_isServer)
            CombatServer.Instance.UnregisterPlayer(_peerId);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_isOwner || _isBot)
            return;

        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);

            _pitchRadians = Mathf.Clamp(
                _pitchRadians - mouseMotion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(MinPitchDegrees),
                Mathf.DegToRad(MaxPitchDegrees));

            var armRotation = _springArm.Rotation;
            armRotation.X = _pitchRadians;
            _springArm.Rotation = armRotation;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }

        if (@event.IsActionPressed("attack_light"))
        {
            TryStab();
        }
    }

    public override void _Process(double delta)
    {
        if (_stabCooldownRemaining > 0f)
            _stabCooldownRemaining = Mathf.Max(0f, _stabCooldownRemaining - (float)delta);

        if (_isRemoteView)
            InterpolateRemote();

        if (_isBot)
            RunBotAi(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isServer)
            RunServerPhysics(delta);
        else if (_isOwner && !_isOffline)
            RunPredictedPhysics(delta);
        else if (_isOffline)
            RunOfflinePhysics(delta);
        // Remote-view clients simulate nothing here — see InterpolateRemote() in _Process.
    }

    // ------------------------------------------------------------------
    // Movement — shared step function, three different drivers
    // ------------------------------------------------------------------

    private void SimulateStep(Vector2 inputDir, double delta)
    {
        var velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= (float)ProjectSettings.GetSetting("physics/3d/default_gravity") * (float)delta;

        var direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        if (direction.LengthSquared() > 0.0001f)
        {
            velocity.X = direction.X * MoveSpeed;
            velocity.Z = direction.Z * MoveSpeed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, MoveSpeed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0f, MoveSpeed);
        }

        Velocity = velocity;
        MoveAndSlide();

        // Safety net: rescue anyone who ends up below the arena instead of free-falling forever.
        // Grey-box collision (CSG use_collision under Jolt) has shown occasional flakiness in
        // testing — this catches that symptom regardless of root cause, and is a reasonable
        // permanent "void" feature for any map anyway.
        if (GlobalPosition.Y < TuningService.Instance.VoidCatchY)
        {
            var spawn = Main.PickRandomSpawn();
            if (spawn is not null)
                GlobalPosition = spawn.GlobalPosition;
            Velocity = Vector3.Zero;
        }
    }

    private void RunOfflinePhysics(double delta)
    {
        var inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        SimulateStep(inputDir, delta);
    }

    private void RunPredictedPhysics(double delta)
    {
        var inputDir = _isBot ? _botMoveInput : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        SimulateStep(inputDir, delta);

        var tick = Engine.GetPhysicsFrames();
        _predictedHistory.Add(new PredictedState(tick, GlobalPosition));
        var maxBuffered = (ulong)(TuningService.Instance.ServerTickRateHz * 2);
        while (_predictedHistory.Count > 0 && tick - _predictedHistory[0].Tick > maxBuffered)
            _predictedHistory.RemoveAt(0);

        var yaw = GlobalRotation.Y;
        Net.Instance.SendWithSimulation(() => RpcId(1, nameof(SubmitInput), tick, inputDir, yaw));
    }

    private void RunServerPhysics(double delta)
    {
        GlobalRotation = new Vector3(GlobalRotation.X, _serverPendingYaw, GlobalRotation.Z);
        SimulateStep(_serverPendingInput, delta);

        var tick = Engine.GetPhysicsFrames();
        Rpc(nameof(ReceiveServerState), tick, GlobalPosition, GlobalRotation.Y);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void SubmitInput(ulong tick, Vector2 inputDir, float yaw)
    {
        if (!_isServer)
            return;
        if (Multiplayer.GetRemoteSenderId() != _peerId)
            return; // reject input claiming to control someone else's node

        _serverPendingInput = inputDir;
        _serverPendingYaw = yaw;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveServerState(ulong tick, Vector3 serverPosition, float serverYaw)
    {
        if (_isServer)
            return;

        if (_isOwner)
            ReconcileOwner(tick, serverPosition);
        else if (_isRemoteView)
        {
            var now = Time.GetTicksMsec() / 1000.0;
            _remoteSnapshots.Add(new RemoteSnapshot(now, serverPosition, serverYaw));
            _remoteSnapshots.RemoveAll(s => now - s.ReceivedAt > 2.0);
        }
    }

    /// <summary>
    /// Simplified reconciliation: a big mismatch (just joined, teleported, hit something the
    /// client didn't predict) snaps hard; small drift is blended in over
    /// Tuning.ReconciliationSmoothTime rather than popping. This corrects the live body
    /// directly rather than doing a full input-replay resimulation — cheaper to write for a
    /// feasibility spike, at the cost of a visible correction on rough connections. Worth
    /// revisiting with real playtest numbers before Phase 2 melee lands on top of it.
    /// </summary>
    private void ReconcileOwner(ulong tick, Vector3 serverPosition)
    {
        var idx = _predictedHistory.FindIndex(s => s.Tick == tick);
        var predictedAtTick = idx >= 0 ? _predictedHistory[idx].Position : GlobalPosition;
        var error = serverPosition.DistanceTo(predictedAtTick);
        var tuning = TuningService.Instance;

        if (error > tuning.ReconciliationSnapDistance)
        {
            GlobalPosition = serverPosition;
            _predictedHistory.Clear();
        }
        else if (error > 0.01f)
        {
            var fixedDelta = 1f / Mathf.Max(1, tuning.ServerTickRateHz);
            var t = 1f - Mathf.Exp(-fixedDelta / Mathf.Max(0.001f, tuning.ReconciliationSmoothTime));
            var target = GlobalPosition + (serverPosition - predictedAtTick);
            GlobalPosition = GlobalPosition.Lerp(target, t);
        }
    }

    /// <summary>Delayed-buffer interpolation toward where the server said this peer was
    /// Tuning.InterpolationDelaySeconds ago — never simulated locally, purely visual.</summary>
    private void InterpolateRemote()
    {
        if (_remoteSnapshots.Count < 2)
            return;

        var renderTime = Time.GetTicksMsec() / 1000.0 - TuningService.Instance.InterpolationDelaySeconds;

        for (var i = _remoteSnapshots.Count - 1; i > 0; i--)
        {
            if (_remoteSnapshots[i - 1].ReceivedAt > renderTime || renderTime > _remoteSnapshots[i].ReceivedAt)
                continue;

            var span = _remoteSnapshots[i].ReceivedAt - _remoteSnapshots[i - 1].ReceivedAt;
            var t = span > 0 ? (float)((renderTime - _remoteSnapshots[i - 1].ReceivedAt) / span) : 0f;
            GlobalPosition = _remoteSnapshots[i - 1].Position.Lerp(_remoteSnapshots[i].Position, t);
            var yaw = Mathf.LerpAngle(_remoteSnapshots[i - 1].YawRadians, _remoteSnapshots[i].YawRadians, t);
            GlobalRotation = new Vector3(GlobalRotation.X, yaw, GlobalRotation.Z);
            return;
        }

        // renderTime is outside the buffered range (just connected, or a spike) — snap to latest known.
        var latest = _remoteSnapshots[^1];
        GlobalPosition = latest.Position;
        GlobalRotation = new Vector3(GlobalRotation.X, latest.YawRadians, GlobalRotation.Z);
    }

    // ------------------------------------------------------------------
    // Network-spike stab verb — temporary, see class doc comment
    // ------------------------------------------------------------------

    private void TryStab()
    {
        if (_stabCooldownRemaining > 0f)
            return;

        _stabCooldownRemaining = TuningService.Instance.StabCooldown;
        PredictedStabCount++;

        Net.Instance.SendWithSimulation(() => RpcId(1, nameof(RequestStab)));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestStab()
    {
        if (!_isServer)
            return;

        var attackerId = Multiplayer.GetRemoteSenderId();
        if (attackerId != _peerId)
            return; // reject a stab request claiming to be from someone else's node

        var forward = -GlobalTransform.Basis.Z.Normalized();
        var rewindSeconds = PingService.Instance.GetOneWayLatencySeconds(attackerId);
        var victimId = CombatServer.Instance.TryResolveStab(attackerId, GlobalPosition, forward, rewindSeconds);

        if (victimId is { } vId)
        {
            CombatServer.Instance.GetPlayerNode(vId)?.ServerApplyStabHit();
            GD.Print($"[CombatServer] Peer {attackerId} stabbed peer {vId} (rewound {rewindSeconds * 1000f:F0}ms).");
        }
        else
        {
            GD.Print($"[CombatServer] Peer {attackerId} stab request: no target in range (rewound {rewindSeconds * 1000f:F0}ms).");
        }

        RpcId(attackerId, nameof(ReceiveStabResult), victimId is not null, victimId ?? -1, rewindSeconds * 1000f);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveStabResult(bool confirmed, long victimId, float rewindMs)
    {
        if (!_isOwner)
            return;

        LastRewindMs = rewindMs;
        if (confirmed)
            ConfirmedStabCount++;
    }

    /// <summary>Server-only. Applies fixed stab damage; on death, resets health and teleports
    /// to a random spawn marker so the spike-test loop keeps going without Phase 2's real
    /// respawn/death-cam system.</summary>
    public void ServerApplyStabHit()
    {
        if (!_isServer)
            return;

        _health -= TuningService.Instance.StabDamage;
        if (_health > 0f)
            return;

        _health = TuningService.Instance.MaxHealth;
        var spawn = Main.PickRandomSpawn();
        if (spawn is not null)
            GlobalPosition = spawn.GlobalPosition;
    }

    // ------------------------------------------------------------------
    // Bot AI — simple wander + periodic stab, see core/Net.cs --bot
    // ------------------------------------------------------------------

    private void PickNewBotTarget()
    {
        var x = (float)GD.RandRange(-16.0, 16.0);
        var z = (float)GD.RandRange(-16.0, 16.0);
        _botTarget = new Vector3(x, GlobalPosition.Y, z);
        _botRetargetIn = GD.RandRange(2.0, 5.0);
    }

    private void RunBotAi(double delta)
    {
        _botRetargetIn -= delta;
        if (_botRetargetIn <= 0 || GlobalPosition.DistanceTo(_botTarget) < 1.0f)
            PickNewBotTarget();

        var toTarget = _botTarget - GlobalPosition;
        toTarget.Y = 0;

        if (toTarget.LengthSquared() > 0.01f)
        {
            var lookBasis = Basis.LookingAt(toTarget.Normalized(), Vector3.Up);
            var desiredYaw = lookBasis.GetEuler().Y;
            GlobalRotation = new Vector3(GlobalRotation.X, Mathf.LerpAngle(GlobalRotation.Y, desiredYaw, 0.1f), GlobalRotation.Z);
            _botMoveInput = new Vector2(0, -1); // "forward" per the move_forward/back convention below
        }
        else
        {
            _botMoveInput = Vector2.Zero;
        }

        _botStabIn -= delta;
        if (_botStabIn <= 0)
        {
            TryStab();
            _botStabIn = GD.RandRange(2.0, 5.0);
        }
    }

    public void SetDisplayName(string playerName)
    {
        if (NameLabel is not null)
            NameLabel.Text = playerName;
    }
}
