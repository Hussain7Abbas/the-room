using System.Collections.Generic;
using Godot;
using TheRoom.Abilities;
using TheRoom.Config;
using TheRoom.Core;

namespace TheRoom.Entities;

/// <summary>
/// Player controller with three distinct simulation roles depending on who's looking at it
/// (see plan/phase-1-network-spike.md):
///   - Server: always runs the ONE authoritative physics + combat simulation, driven by the
///     latest input received from the owning client.
///   - Owning client: predicts movement locally from real input (zero perceived latency),
///     sends that input to the server, and reconciles toward the server's periodic
///     corrections when they disagree. Combat verbs are NOT predicted — they go straight to
///     the server and wait for the result (Pillar 2: every hit must be explainable, so a
///     locally-guessed hit that gets overruled would be exactly the wrong kind of "fun").
///   - Every other client (watching a peer that isn't them and isn't the server): runs no
///     physics at all — just interpolates visually between buffered server snapshots,
///     delayed by Tuning.InterpolationDelaySeconds.
/// Offline mode (no MultiplayerPeer) skips all of the above and behaves like a single predicting
/// client with no server round-trip at all — combat resolves locally and instantly.
///
/// Combat (Phase 2, GDD §5.2 / CHARACTER-SPEC.md grammar): a server-authoritative state machine
/// per player — Idle → {LightWindup, HeavyWindup, Parrying} → Recovery/Staggered/Executing →
/// Idle, plus a terminal Dead state. Hit resolution reuses the rewind lag-compensation proven in
/// Phase 1 (core/CombatServer.cs), parameterized per verb.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float MoveSpeed = 6.0f;
    [Export] public float MouseSensitivity = 0.0035f;
    [Export] public float MinPitchDegrees = -60f;
    [Export] public float MaxPitchDegrees = 70f;

    [Export] public NodePath SpringArmPath = "CameraPivot/SpringArm3D";
    [Export] public NodePath MeshPath = "MeshInstance3D";
    [Export] public Label3D? NameLabel;

    private SpringArm3D _springArm = null!;
    private MeshInstance3D? _meshInstance;
    private StandardMaterial3D? _meshMaterial;
    private float _pitchRadians;

    // --- role, decided once in _Ready ---
    private bool _isServer;
    private bool _isOffline;
    private bool _isOwner;      // this machine predicts/controls this node (owning client, or offline local player)
    private bool _isRemoteView; // another client watching a peer that is neither them nor the server
    private bool _isBot;
    private long _peerId;
    public long PeerId => _peerId;

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

    // --- combat state machine (authoritative on the server; every peer's own copy of ITS OWN
    // node also mirrors it locally purely to gate re-sending an input the server will reject
    // anyway — never trusted for hit outcomes) ---
    private enum CombatState { Idle, LightWindup, HeavyWindup, Recovery, Parrying, Staggered, Executing, Dead }
    private enum Verb { Light, Heavy, Parry, Dash }

    private CombatState _combatState = CombatState.Idle;
    private float _stateTimer;
    private float _parryCooldownRemaining;
    private float _dashCooldownRemaining;
    private float _spawnProtectionRemaining;
    private float _health;

    // Dash is a pure movement override, independent of _combatState (GDD §5.1: spacing tool,
    // no i-frames, doesn't interact with the attack grammar at all).
    private Vector3 _dashVelocity;
    private float _dashTimeRemaining;

    public bool IsDead => _combatState == CombatState.Dead;
    public bool IsSpawnProtected => _spawnProtectionRemaining > 0f;
    public float HealthFraction => Mathf.Clamp(_health / Mathf.Max(1f, TuningService.Instance.MaxHealth), 0f, 1f);

    // --- bot AI (owning client with --bot; see core/Net.cs) ---
    private Vector2 _botMoveInput;
    private Vector3 _botTarget;
    private double _botRetargetIn;
    private double _botVerbIn;
    private double _botAbilityIn;

    // --- debug overlay stats (owning client only, see core/DebugOverlay.cs) ---
    public int AttackRequestCount { get; private set; }
    public int ConfirmedHitCount { get; private set; }
    public float LastRewindMs { get; private set; }
    public bool IsLocallyControlled => _isOwner;
    public bool IsBotControlled => _isBot;
    public string RoleLabel => _isServer ? "Server" : _isOffline ? "Offline" : _isBot ? "Bot" : _isOwner ? "Client (owner)" : "Remote view";

    // --- death cam (owning client only) ---
    private float _deathCamRemaining;
    private Vector3 _deathCamLookAt;

    // --- character/ability (Phase 3, CHARACTER-SPEC.md) ---
    private CharacterDef? _characterDef;
    private Ability? _ability;
    private string _displayName = "";
    public string DisplayName => _displayName;
    public CharacterDef? Character => _characterDef;

    // Ability shared-kit state (abilities/Ability.cs ApplySlow/Reveal helpers write these).
    private float _slowMultiplier = 1f;
    private float _slowTimeRemaining;
    private float _revealRemaining;

    public override void _Ready()
    {
        _springArm = GetNode<SpringArm3D>(SpringArmPath);
        _meshInstance = GetNodeOrNull<MeshInstance3D>(MeshPath);
        if (_meshInstance?.GetActiveMaterial(0) is StandardMaterial3D baseMat)
        {
            _meshMaterial = (StandardMaterial3D)baseMat.Duplicate();
            _meshInstance.SetSurfaceOverrideMaterial(0, _meshMaterial);
        }

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

            _spawnProtectionRemaining = TuningService.Instance.SpawnProtectionDuration;
        }

        // Identity handshake: the owning client tells the server its chosen name + character;
        // the server validates and broadcasts the result to everyone (this node's Name label,
        // Main's peer-id->name registry for killfeed/scoreboard, and the equipped ability all
        // depend on every peer — not just the server — actually receiving this). Before this,
        // SetDisplayName() was only ever called locally on the server's own copy, which
        // MultiplayerSpawner never replicates — a real bug: every client's name label and
        // killfeed/scoreboard entries silently stayed blank/placeholder for anyone but the
        // server. See plan/phase-3-abilities.md.
        if (_isOffline)
        {
            ApplyIdentity(Net.Instance.LocalPlayerName, Net.Instance.ChosenCharacterId);
        }
        else if (_isOwner)
        {
            RpcId(1, nameof(AnnounceIdentity), Net.Instance.LocalPlayerName, Net.Instance.ChosenCharacterId ?? "");
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

        if (IsDead)
            return; // no verbs while dead/in death cam

        if (@event.IsActionPressed("attack_light")) RequestVerbLocal(Verb.Light);
        if (@event.IsActionPressed("attack_heavy")) RequestVerbLocal(Verb.Heavy);
        if (@event.IsActionPressed("parry")) RequestVerbLocal(Verb.Parry);
        if (@event.IsActionPressed("dash")) RequestVerbLocal(Verb.Dash);
        if (@event.IsActionPressed("ability")) RequestAbilityLocal();
    }

    public override void _Process(double delta)
    {
        if (_isRemoteView)
            InterpolateRemote();

        if (_isBot)
            RunBotAi(delta);

        if (_isOwner && _deathCamRemaining > 0f)
            RunDeathCam(delta);

        UpdateVisualEffects(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        // QueueFree() (disconnect handling, Main.OnPlayerDisconnected) defers actual removal to
        // end-of-frame — a node can still get one more _PhysicsProcess tick after being queued
        // for removal, and GlobalPosition/Rpc() on a node no longer in the tree logs a Godot
        // engine error. Cheap, standard guard for that race.
        if (!IsInsideTree())
            return;

        if (_isServer)
        {
            TickCombatState((float)delta);
            _ability?.Tick((float)delta);
            RunServerPhysics(delta);
        }
        else if (_isOwner && !_isOffline)
        {
            RunPredictedPhysics(delta);
        }
        else if (_isOffline)
        {
            TickCombatState((float)delta); // offline: this instance is also its own authority
            _ability?.Tick((float)delta);
            RunOfflinePhysics(delta);
        }
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
        else if (TuningService.Instance.HopEnabled && WantsHop())
            velocity.Y = TuningService.Instance.HopImpulse;

        if (_dashTimeRemaining > 0f)
        {
            // Dash overrides normal horizontal input entirely for its duration — pure spacing
            // burst, no i-frames (GDD §5.1: an i-framed dash + network latency = unexplainable
            // deaths, banned by Pillar 2).
            velocity.X = _dashVelocity.X;
            velocity.Z = _dashVelocity.Z;
            _dashTimeRemaining -= (float)delta;
        }
        else
        {
            var direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
            var effectiveSpeed = MoveSpeed * _slowMultiplier; // ability shared-kit: Ability.ApplySlow

            if (direction.LengthSquared() > 0.0001f)
            {
                velocity.X = direction.X * effectiveSpeed;
                velocity.Z = direction.Z * effectiveSpeed;
            }
            else
            {
                velocity.X = Mathf.MoveToward(velocity.X, 0f, effectiveSpeed);
                velocity.Z = Mathf.MoveToward(velocity.Z, 0f, effectiveSpeed);
            }
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

    private bool WantsHop() => _isBot ? false : Input.IsActionJustPressed("jump");

    private void RunOfflinePhysics(double delta)
    {
        if (IsDead) return;
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
        if (!IsDead)
            GlobalRotation = new Vector3(GlobalRotation.X, _serverPendingYaw, GlobalRotation.Z);

        if (!IsDead)
            SimulateStep(_serverPendingInput, delta);

        var tick = Engine.GetPhysicsFrames();
        Rpc(nameof(ReceiveServerState), tick, GlobalPosition, GlobalRotation.Y);

        if (_spawnProtectionRemaining > 0f)
            _spawnProtectionRemaining = Mathf.Max(0f, _spawnProtectionRemaining - (float)delta);
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
    /// revisiting with real playtest numbers as Phase 2 combat puts more weight on precise
    /// positioning (dash spacing, execute's behind-the-back check).
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
    // Combat — verb requests (client -> server), state machine (server only)
    // ------------------------------------------------------------------

    private void RequestVerbLocal(Verb verb)
    {
        AttackRequestCount++;

        if (_isOffline)
        {
            // No server round-trip offline — this instance IS its own authority.
            TryStartVerb(verb);
            return;
        }

        Net.Instance.SendWithSimulation(() => RpcId(1, nameof(RequestVerb), (int)verb));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestVerb(int verbInt)
    {
        if (!_isServer)
            return;
        if (Multiplayer.GetRemoteSenderId() != _peerId)
            return; // reject a verb claiming to be from someone else's node

        TryStartVerb((Verb)verbInt);
    }

    private void TryStartVerb(Verb verb)
    {
        if (_combatState == CombatState.Dead)
            return;

        // Any deliberate action cancels spawn protection immediately (GDD §5.7: "cancelled
        // instantly on attacking"). Dash counts too — using the shimmer window to reposition
        // for free would be the obvious abuse case a playtester would find first.
        _spawnProtectionRemaining = 0f;

        switch (verb)
        {
            case Verb.Light:
                if (_combatState != CombatState.Idle) return;
                _combatState = CombatState.LightWindup;
                _stateTimer = TuningService.Instance.LightWindup;
                break;

            case Verb.Heavy:
                if (_combatState != CombatState.Idle) return;
                _combatState = CombatState.HeavyWindup;
                _stateTimer = TuningService.Instance.HeavyWindup;
                break;

            case Verb.Parry:
                if (_combatState != CombatState.Idle) return;
                if (_parryCooldownRemaining > 0f) return;
                _combatState = CombatState.Parrying;
                _stateTimer = TuningService.Instance.ParryActiveWindow;
                _parryCooldownRemaining = TuningService.Instance.ParryCooldown; // starts on attempt, hit or not
                break;

            case Verb.Dash:
                if (_combatState != CombatState.Idle) return; // recovery locks out your own escape too — that's what makes whiffing costly
                if (_dashCooldownRemaining > 0f) return;
                _dashCooldownRemaining = TuningService.Instance.DashCooldown;
                var forward = -GlobalTransform.Basis.Z.Normalized();
                var tuning = TuningService.Instance;
                _dashVelocity = forward * (tuning.DashDistance / Mathf.Max(0.01f, tuning.DashDuration));
                _dashTimeRemaining = tuning.DashDuration;
                break;
        }
    }

    /// <summary>Server-only (and offline-solo): advances windup/recovery/parry/stagger/execute
    /// timers and resolves the attack the instant a windup completes.</summary>
    private void TickCombatState(float delta)
    {
        if (_parryCooldownRemaining > 0f)
            _parryCooldownRemaining = Mathf.Max(0f, _parryCooldownRemaining - delta);
        if (_dashCooldownRemaining > 0f)
            _dashCooldownRemaining = Mathf.Max(0f, _dashCooldownRemaining - delta);

        if (_slowTimeRemaining > 0f)
        {
            _slowTimeRemaining = Mathf.Max(0f, _slowTimeRemaining - delta);
            if (_slowTimeRemaining <= 0f)
                _slowMultiplier = 1f;
        }
        if (_revealRemaining > 0f)
            _revealRemaining = Mathf.Max(0f, _revealRemaining - delta);

        if (_combatState is CombatState.Idle or CombatState.Dead)
            return;

        _stateTimer -= delta;
        if (_stateTimer > 0f)
            return;

        switch (_combatState)
        {
            case CombatState.LightWindup:
                ResolveMeleeAttack(isHeavy: false);
                _combatState = CombatState.Recovery;
                _stateTimer = TuningService.Instance.LightRecovery;
                break;

            case CombatState.HeavyWindup:
                ResolveMeleeAttack(isHeavy: true);
                _combatState = CombatState.Recovery;
                _stateTimer = TuningService.Instance.HeavyRecovery;
                break;

            case CombatState.Recovery:
            case CombatState.Parrying:  // window closed with no parry — "failed parry leaves you open"
            case CombatState.Staggered:
            case CombatState.Executing:
                _combatState = CombatState.Idle;
                break;
        }
    }

    private void ResolveMeleeAttack(bool isHeavy)
    {
        var tuning = TuningService.Instance;
        var forward = -GlobalTransform.Basis.Z.Normalized();

        if (isHeavy)
        {
            // The lunge itself: close the gap toward whatever's in front, stopping on collision
            // so it can't be used to phase through walls/pillars.
            MoveAndCollide(forward * tuning.HeavyLungeRange);
        }

        var range = isHeavy ? tuning.HeavyLungeRange : tuning.LightRange;
        var radius = isHeavy ? tuning.HeavyHitRadius : tuning.LightHitRadius;
        var rewindSeconds = PingService.Instance.GetOneWayLatencySeconds(_peerId);

        var victimId = CombatServer.Instance.TryResolveMeleeHit(_peerId, GlobalPosition, forward, range, radius, rewindSeconds);
        LastRewindMs = rewindSeconds * 1000f;

        if (victimId is not { } vId)
        {
            RpcId(_peerId, nameof(ReceiveAttackResult), false, -1L);
            return;
        }

        var victim = CombatServer.Instance.GetPlayerNode(vId);
        if (victim is null)
        {
            RpcId(_peerId, nameof(ReceiveAttackResult), false, -1L);
            return;
        }

        // Parry check: victim currently has an active parry window open.
        if (victim._combatState == CombatState.Parrying)
        {
            victim._combatState = CombatState.Idle; // consumed — successful parry, no extra cooldown beyond the one already ticking
            _combatState = CombatState.Staggered;
            _stateTimer = tuning.ParryStaggerDuration;
            GD.Print($"[Combat] {Main.GetPlayerName(vId)} parried {Main.GetPlayerName(_peerId)}'s {(isHeavy ? "heavy" : "light")}.");
            RpcId(_peerId, nameof(ReceiveAttackResult), false, vId);
            return;
        }

        if (victim.IsSpawnProtected)
        {
            RpcId(_peerId, nameof(ReceiveAttackResult), false, vId);
            return; // shimmer means shimmer — no damage, no execute, nothing
        }

        // Execute check (heavy only): attacker is directly behind the victim's own facing.
        var isExecute = false;
        if (isHeavy)
        {
            var victimRewound = CombatServer.Instance.GetRewoundPosition(vId, rewindSeconds) ?? victim.GlobalPosition;
            var victimForward = -victim.GlobalTransform.Basis.Z.Normalized();
            var victimToAttacker = GlobalPosition - victimRewound;
            if (victimToAttacker.LengthSquared() > 0.0001f)
            {
                // Angle between where the victim is FACING and where the attacker IS: near 180°
                // means the attacker is behind the victim's back, not in front of their face.
                var angle = Mathf.RadToDeg(victimForward.AngleTo(victimToAttacker.Normalized()));
                isExecute = angle > (180f - tuning.ExecuteBehindAngleDegrees);
            }
        }

        if (isExecute)
        {
            victim.ServerApplyDamage(victim._health, _peerId, "execute");
            _combatState = CombatState.Executing;
            _stateTimer = tuning.ExecuteAnimationLock; // "suicidal in a crowd" — you're locked and exposed right after
        }
        else
        {
            var damage = tuning.MaxHealth * (isHeavy ? tuning.HeavyDamagePercent : tuning.LightDamagePercent);
            victim.ServerApplyDamage(damage, _peerId, isHeavy ? "heavy" : "light");
            if (isHeavy && !victim.IsDead) // don't resurrect a kill into a stagger
            {
                victim._combatState = CombatState.Staggered;
                victim._stateTimer = tuning.HeavyStaggerDuration;
            }
        }

        RpcId(_peerId, nameof(ReceiveAttackResult), true, vId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveAttackResult(bool confirmed, long victimId)
    {
        if (!_isOwner)
            return;

        if (confirmed)
            ConfirmedHitCount++;
    }

    /// <summary>Server-only. Applies damage; on lethal, kills, broadcasts the kill (killfeed +
    /// death cam trigger), and schedules a respawn. GDD §5.2 target TTK: 2-3 connected hits.</summary>
    public void ServerApplyDamage(float amount, long attackerId, string method)
    {
        if (!_isServer || IsDead || !IsInsideTree())
            return;

        _health -= amount;
        if (_health > 0f)
            return;

        _health = 0f;
        _combatState = CombatState.Dead;
        Velocity = Vector3.Zero;

        GD.Print($"[Combat] {Main.GetPlayerName(attackerId)} killed {Main.GetPlayerName(_peerId)} ({method}).");

        // Broadcast from the VICTIM node (this) to everyone — killfeed + this player's own
        // death cam trigger on their own client (see BroadcastKill).
        Rpc(nameof(BroadcastKill), attackerId, _peerId, method, GlobalPosition);

        GetTree().CreateTimer(TuningService.Instance.RespawnTime).Timeout += ServerRespawn;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BroadcastKill(long killerId, long victimId, string method, Vector3 deathPosition)
    {
        // CallLocal is on so this also fires for the server's own call (needed for offline mode,
        // where "the server" and "the only client" are the same process). GetRemoteSenderId()
        // reads 0 for that local invocation, so only gate remote calls, and only the real server
        // (peer 1) is ever allowed to have actually sent one in the first place.
        if (!_isServer && Multiplayer.GetRemoteSenderId() != 1)
            return;

        Events.Instance.EmitSignal(Events.SignalName.PlayerKilled, killerId, victimId, method);
        SpawnDeathEffect(deathPosition);

        if (_isOwner && victimId == _peerId)
        {
            _deathCamRemaining = TuningService.Instance.DeathCamDuration;
            var killer = CombatServer.Instance.GetPlayerNode(killerId);
            _deathCamLookAt = killer is not null ? killer.GlobalPosition : deathPosition;
        }
    }

    private void ServerRespawn()
    {
        if (!_isServer || !IsDead)
            return;

        _health = TuningService.Instance.MaxHealth;
        _combatState = CombatState.Idle;
        Velocity = Vector3.Zero;

        var spawn = Main.PickRandomSpawn();
        if (spawn is not null)
            GlobalPosition = spawn.GlobalPosition;

        _spawnProtectionRemaining = TuningService.Instance.SpawnProtectionDuration;
    }

    // ------------------------------------------------------------------
    // Identity (name + character) — see the _Ready() handshake comment for why this exists
    // ------------------------------------------------------------------

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AnnounceIdentity(string requestedName, string? characterId)
    {
        if (!_isServer)
            return;
        if (Multiplayer.GetRemoteSenderId() != _peerId)
            return; // reject an identity claim for someone else's node

        var safeName = string.IsNullOrWhiteSpace(requestedName) ? $"Player {_peerId}" : requestedName;
        Rpc(nameof(ReceiveIdentity), safeName, characterId ?? "");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveIdentity(string name, string? characterId)
    {
        if (!_isServer && Multiplayer.GetRemoteSenderId() != 1)
            return;

        ApplyIdentity(name, characterId);
    }

    private void ApplyIdentity(string name, string? characterId)
    {
        _displayName = name;
        SetDisplayName(name);
        Main.RegisterPlayerName(_peerId, name);
        EquipCharacter(characterId);
    }

    private void EquipCharacter(string? characterId)
    {
        _characterDef = CharacterRegistry.GetOrDefault(characterId);
        _ability = CreateAbility(_characterDef.Ability, this);

        if (_meshMaterial is not null)
            _meshMaterial.AlbedoColor = _characterDef.SilhouetteColor;
    }

    private static Ability? CreateAbility(AbilityDef? def, Player caster) => def?.Id switch
    {
        "blink" => new BlinkAbility(def, caster),
        "firepatch" => new FirePatchAbility(def, caster),
        _ => null,
    };

    // ------------------------------------------------------------------
    // Ability activation (client -> server), separate timer from the melee state machine —
    // see abilities/Ability.cs's class doc for why they're decoupled
    // ------------------------------------------------------------------

    private void RequestAbilityLocal()
    {
        if (_combatState != CombatState.Idle)
            return;

        _spawnProtectionRemaining = 0f; // cancelled instantly on any deliberate action, same as the melee verbs

        if (_isOffline)
        {
            _ability?.TryActivate();
            return;
        }

        Net.Instance.SendWithSimulation(() => RpcId(1, nameof(RequestAbility)));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestAbility()
    {
        if (!_isServer)
            return;
        if (Multiplayer.GetRemoteSenderId() != _peerId)
            return;
        if (_combatState != CombatState.Idle)
            return;

        _ability?.TryActivate();
    }

    /// <summary>Called by Ability.TryActivate() the instant a windup starts — broadcasts a
    /// visible tell on the caster (grey-box stand-in: an emissive flash, see
    /// UpdateVisualEffects) to every client, since only the server ever runs ability logic and
    /// nothing here replicates automatically.</summary>
    public void BroadcastAbilityTell(string abilityId, float tellSeconds)
    {
        if (!_isServer)
            return;
        Rpc(nameof(ReceiveAbilityTell), tellSeconds);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveAbilityTell(float tellSeconds)
    {
        if (!_isServer && Multiplayer.GetRemoteSenderId() != 1)
            return;
        _abilityTellRemaining = tellSeconds;
    }
    private float _abilityTellRemaining;

    // ------------------------------------------------------------------
    // Ability shared-kit support (CHARACTER-SPEC.md Part 1: abilities build on these, they
    // don't invent their own systems) — called from abilities/Ability.cs's protected helpers
    // ------------------------------------------------------------------

    public void ServerTeleport(Vector3 position)
    {
        if (!_isServer)
            return;
        GlobalPosition = position;
    }

    public void ServerApplyDisplacement(Vector3 impulse)
    {
        if (!_isServer)
            return;
        Velocity += impulse;
    }

    /// <summary>Duration is expected pre-clamped by Ability.ApplySlow (≤ Tuning.MaxControlEffectDuration).
    /// Multiple overlapping slows take the strongest (lowest) multiplier rather than stacking multiplicatively.</summary>
    public void ServerApplySlow(float speedMultiplier, float durationSeconds)
    {
        if (!_isServer)
            return;
        _slowMultiplier = Mathf.Min(_slowMultiplier, speedMultiplier);
        _slowTimeRemaining = Mathf.Max(_slowTimeRemaining, durationSeconds);
    }

    public void ServerApplyReveal(float durationSeconds)
    {
        if (!_isServer)
            return;
        Rpc(nameof(BroadcastReveal), durationSeconds);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void BroadcastReveal(float durationSeconds)
    {
        if (!_isServer && Multiplayer.GetRemoteSenderId() != 1)
            return;
        _revealRemaining = durationSeconds;
    }

    /// <summary>Zone Denial / Trap shared kit (Ability.SpawnDamageZone): broadcasts from the
    /// caster so every client spawns its own AbilityZone — visible everywhere, damage only ever
    /// real on the server copy (see AbilityZone's own IsServer guard).</summary>
    public void ServerSpawnDamageZone(Vector3 position, float radius, float durationSeconds, float damagePerSecond, string method, Color color)
    {
        if (!_isServer)
            return;
        Rpc(nameof(BroadcastAbilityZone), position, radius, durationSeconds, damagePerSecond, method, color);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BroadcastAbilityZone(Vector3 position, float radius, float durationSeconds, float damagePerSecond, string method, Color color)
    {
        if (!_isServer && Multiplayer.GetRemoteSenderId() != 1)
            return;

        var attackerId = _peerId; // this node IS the caster
        AbilityZone.Spawn(position, radius, durationSeconds, color, oneShot: false, (victim, delta) =>
        {
            victim.ServerApplyDamage(damagePerSecond * delta, attackerId, method);
        });
    }

    /// <summary>Cosmetic-only "ragdoll": a primitive tumbling capsule dropped where the player
    /// died, no networking, each client spawns its own on receiving BroadcastKill. Grey-box
    /// stand-in per plan/phase-2-greybox-combat.md — a real skinned ragdoll waits for Phase 5
    /// character art. Self-frees after a few seconds.</summary>
    private static void SpawnDeathEffect(Vector3 position)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = tree.CurrentScene;
        if (root is null) return;

        // Set local Position, not GlobalPosition: the node has no parent yet, and
        // GlobalPosition's setter reads the current global transform to compute a relative
        // offset — on a node not yet in the tree that read fails (logs a harmless-but-noisy
        // engine error and falls back to identity). Local == global with no parent, so this is
        // equivalent and avoids the tree dependency entirely.
        var body = new RigidBody3D { Position = position + Vector3.Up * 0.3f };
        var shape = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.6f } };
        var mesh = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.6f } };
        body.AddChild(shape);
        body.AddChild(mesh);
        root.AddChild(body);

        var rng = new RandomNumberGenerator();
        body.ApplyImpulse(new Vector3(rng.RandfRange(-2f, 2f), rng.RandfRange(1f, 3f), rng.RandfRange(-2f, 2f)));
        body.ApplyTorqueImpulse(new Vector3(rng.RandfRange(-3f, 3f), rng.RandfRange(-3f, 3f), rng.RandfRange(-3f, 3f)));

        tree.CreateTimer(3.0).Timeout += () => { if (IsInstanceValid(body)) body.QueueFree(); };
    }

    private void RunDeathCam(double delta)
    {
        _deathCamRemaining = Mathf.Max(0f, _deathCamRemaining - (float)delta);

        var toKiller = _deathCamLookAt - GlobalPosition;
        toKiller.Y = 0;
        if (toKiller.LengthSquared() > 0.01f)
        {
            var lookBasis = Basis.LookingAt(toKiller.Normalized(), Vector3.Up);
            GlobalRotation = new Vector3(GlobalRotation.X, lookBasis.GetEuler().Y, GlobalRotation.Z);
        }
    }

    /// <summary>Grey-box stand-in for every "needs a visible tell/state" requirement that
    /// doesn't have a real VFX asset yet (spawn protection shimmer GDD §5.7; an ability's tell,
    /// CHARACTER-SPEC.md Part 1; being Revealed by an Information-slot ability) — an emissive
    /// color flash on the same mesh material. Real shader work is Phase 5 art-pass territory.
    /// Priority when more than one is active at once: ability tell, then reveal, then spawn
    /// protection — arbitrary but consistent, and rare to actually overlap.</summary>
    private void UpdateVisualEffects(double delta)
    {
        if (_abilityTellRemaining > 0f)
            _abilityTellRemaining = Mathf.Max(0f, _abilityTellRemaining - (float)delta);

        if (_meshMaterial is null)
            return;

        Color? color = _abilityTellRemaining > 0f ? new Color(0.2f, 0.9f, 1f) // cyan
            : _revealRemaining > 0f ? new Color(1f, 0.15f, 0.15f) // red
            : _spawnProtectionRemaining > 0f ? new Color(1f, 1f, 1f) // white
            : null;

        _meshMaterial.EmissionEnabled = color is not null;
        if (color is { } c)
            _meshMaterial.Emission = c;
    }

    // ------------------------------------------------------------------
    // Bot AI — wander + occasional verbs, see core/Net.cs --bot
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
        if (IsDead)
        {
            _botMoveInput = Vector2.Zero;
            return;
        }

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

        _botVerbIn -= delta;
        if (_botVerbIn <= 0)
        {
            // Weighted toward light (cheap, spammable) with occasional heavy/parry/dash — rough
            // stand-in for "a bot that presses buttons", not remotely competent play.
            var roll = GD.Randf();
            var verb = roll switch
            {
                < 0.55f => Verb.Light,
                < 0.80f => Verb.Heavy,
                < 0.92f => Verb.Dash,
                _ => Verb.Parry,
            };
            RequestVerbLocal(verb);
            _botVerbIn = GD.RandRange(1.0, 3.0);
        }

        _botAbilityIn -= delta;
        if (_botAbilityIn <= 0)
        {
            RequestAbilityLocal();
            _botAbilityIn = GD.RandRange(4.0, 10.0);
        }
    }

    public void SetDisplayName(string playerName)
    {
        if (NameLabel is not null)
            NameLabel.Text = playerName;
    }
}
