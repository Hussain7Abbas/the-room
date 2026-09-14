# Deployment (the live server)

This page covers **updating the server that already runs The Room**. To install on a new machine,
see [Server configuration](server-config.md).

## What runs in production

| Piece | Where |
|---|---|
| Host | a Ubuntu 24.04 VPS, SSH alias `kios-chat`, **shared with other apps** |
| Game code | `/opt/the-room/app` (rsynced from the repo, built there) |
| Lobby | `/opt/the-room/lobby` (`dotnet publish` output) as systemd unit **`the-room-lobby`** |
| Rooms | child processes of the lobby: main room on UDP **60010**, created rooms on **60011–60030** |
| Match history | `/opt/the-room/data/lobby.db` |
| .NET 8 SDK | `/opt/the-room/dotnet` |
| Godot 4.7.2 mono (Linux) | `/opt/the-room/godot-app/` |
| HTTP API | nginx site `room-api.iscoded.com` (Cloudflare-proxied) → lobby on `127.0.0.1:5310` |
| Gameplay hostname | `room-udp.iscoded.com` (DNS-only, raw UDP) |

Everything runs as the unprivileged system user **`theroom`**. Never touch the box's other apps,
nginx sites or firewall.

## Deploy

```bash
make test            # first
make deploy-server   # code + lobby
make deploy-nginx    # only when deploy/nginx/room-api.iscoded.com.conf changed
```

`make deploy-server` does the following:

1. It **rsyncs** the repo to `/opt/the-room/app` with `--delete`. It excludes `.git`, `.godot`,
   `bin`, `obj`, `build`, `.lobby`, `*.db` and `season_stats.json`, so server-side data is never
   overwritten.
2. As `theroom`, it runs `dotnet build "The Room.sln"` and `dotnet publish services/lobby -c
   Release -o /opt/the-room/lobby`.
3. It installs `deploy/systemd/the-room-lobby.service` and runs `daemon-reload`. The first time,
   it also retires the old `the-room-server.service`.
4. It restarts `the-room-lobby`, waits for `/api/health`, and prints `active` plus
   `{"status":"ok"}`.

Restarting the lobby restarts **every room**, so players in matches get disconnected. Deploy when
rooms are quiet (check `curl -s https://room-api.iscoded.com/api/rooms`).

`make deploy-nginx` streams the config over SSH and installs it. If `nginx -t` fails it restores
the previous file, and it only reloads nginx on success.

Clients connect to the servers named by the lobby and need no deploy. After game-protocol changes
(new or changed RPCs), players must update their build too, because old and new clients can't
share a room.

## Checking on it

```bash
make deploy-status                     # systemctl status the-room-lobby
make deploy-logs                       # last 100 journal lines (rooms' logs are prefixed [CODE])
curl -s https://room-api.iscoded.com/api/rooms
curl -s 'https://room-api.iscoded.com/api/matches?pageSize=3'
```

## Rolling back

```bash
git checkout <good-commit>
make deploy-server
git checkout main
```

The database schema only ever grows, so an older lobby can read a newer `lobby.db`.
