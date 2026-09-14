using System.Collections.Generic;
using Godot;
using TheRoom.Entities;

namespace TheRoom.Core;

/// <summary>
/// Root scene (Main.tscn): loads the grey-box room and spawns a Player under PlayersContainer
/// for every connected peer. A MultiplayerSpawner (configured in Main.tscn) watches
/// PlayersContainer and replicates spawns/despawns to clients automatically — no manual RPCs
/// needed for this. Also runs fully offline for solo local testing (see core/Net.cs).
/// </summary>
public partial class Main : Node3D
{
    private const string PlayerScenePath = "res://player/Player.tscn";

    private Node3D _playersContainer = null!;
    private readonly List<Marker3D> _spawnMarkers = new();
    private int _nextSpawnIndex;

    public override void _Ready()
    {
        _playersContainer = GetNode<Node3D>("PlayersContainer");

        var spawnPoints = GetNodeOrNull<Node3D>("Room/SpawnPoints");
        if (spawnPoints is not null)
        {
            foreach (var child in spawnPoints.GetChildren())
            {
                if (child is Marker3D marker)
                    _spawnMarkers.Add(marker);
            }
        }

        Events.Instance.PlayerConnected += OnPlayerConnected;
        Events.Instance.PlayerDisconnected += OnPlayerDisconnected;

        // The server (and offline mode) spawns its own local player as peer id 1 immediately —
        // PeerConnected only fires for *remote* peers, never for yourself.
        if (Net.Instance.IsServer || Net.Instance.IsOffline)
        {
            SpawnPlayer(1);
        }
    }

    private void OnPlayerConnected(long peerId)
    {
        if (!Net.Instance.IsServer)
            return; // only the server spawns authoritative player nodes; clients receive them via MultiplayerSpawner

        SpawnPlayer(peerId);
    }

    private void OnPlayerDisconnected(long peerId)
    {
        if (!Net.Instance.IsServer)
            return;

        var node = _playersContainer.GetNodeOrNull(peerId.ToString());
        node?.QueueFree();
    }

    private void SpawnPlayer(long peerId)
    {
        var scene = GD.Load<PackedScene>(PlayerScenePath);
        var player = scene.Instantiate<Player>();
        player.Name = peerId.ToString();
        player.SetMultiplayerAuthority((int)peerId);

        _playersContainer.AddChild(player, forceReadableName: true);

        if (_spawnMarkers.Count > 0)
        {
            var spawn = _spawnMarkers[_nextSpawnIndex++ % _spawnMarkers.Count];
            player.GlobalTransform = spawn.GlobalTransform;
        }

        var isLocalPlayer = Net.Instance.IsOffline || peerId == Multiplayer.GetUniqueId();
        player.SetDisplayName(isLocalPlayer ? Net.Instance.LocalPlayerName : $"Player {peerId}");
    }
}
