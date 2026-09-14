# Server configuration: installing on a new server

This guide installs the complete backend on a **fresh Linux server**: the lobby, the room game
servers, match history storage and the public HTTPS API. It's the same setup as production (see
[Deployment](deployment.md)). Follow it top to bottom.

Placeholders used below. Replace them with your own values:

| Placeholder | Example | Meaning |
|---|---|---|
| `<SERVER_IP>` | — | the server's public IPv4 (**never commit it to the repo**) |
| `<API_HOST>` | `room-api.example.com` | HTTPS API hostname (can sit behind Cloudflare's proxy) |
| `<UDP_HOST>` | `room-udp.example.com` | gameplay hostname (**must be DNS-only**) |
| `<EMAIL>` | `ops@example.com` | for Let's Encrypt notices |

## 0. What you'll end up with

```
/opt/the-room/                 home of the unprivileged system user "theroom"
├── app/                       the repo (game project), built in place
├── lobby/                     published lobby service (TheRoom.Lobby.dll)
├── data/lobby.db              match history (SQLite)
├── dotnet/                    .NET 8 SDK (private to this app)
└── godot-app/                 Godot 4.7.2 mono for Linux (headless)

systemd:  the-room-lobby.service      → lobby on 127.0.0.1:5310
                                      → starts room servers on UDP 60010 (main) and 60011–60030
nginx:    <API_HOST> :443             → /api/ proxied to the lobby
```

## 1. Requirements

- **OS:** Ubuntu 24.04 LTS x86_64 (production runs 24.04.3); 22.04 also works. Other distros
  work with their own package names.
- **Size:** 2 vCPU and **4 GB RAM** recommended. Each room's game server uses about 210 MB and
  the lobby about 80 MB. A 2 GB machine can run the main room plus 2 created rooms
  (`MAX_ROOMS=2`).
- **Access:** a public IPv4 address, SSH as root (or a sudo user), and a domain where you can
  create two DNS records.
- **Outbound internet** during setup, to download .NET, Godot and NuGet packages.

The Godot Linux binary needs **no extra system libraries** in headless mode: on stock Ubuntu 24.04,
`ldd` reports nothing missing.

## 2. DNS

Create two **A records** pointing at `<SERVER_IP>`:

| Record | Cloudflare proxy | Why |
|---|---|---|
| `<API_HOST>` | **Proxied** (orange) is fine | plain HTTPS; Cloudflare can cache and protect it |
| `<UDP_HOST>` | **DNS only** (grey) — required | game traffic is raw UDP (ENet); Cloudflare's proxy only carries HTTP, so a proxied record silently breaks joining |

If you use Cloudflare, set **SSL/TLS mode = Full (strict)** once the certificate from step 9
exists.

Check it: `dig +short <UDP_HOST>` must return `<SERVER_IP>` itself, not a Cloudflare address.

## 3. Base packages

```bash
sudo apt update
sudo apt install -y nginx certbot python3-certbot-nginx rsync unzip curl git sqlite3
```

`sqlite3` is optional; it's only for backups and inspection.

## 4. Firewall

Open **SSH first**, then HTTP/HTTPS and the game's UDP range:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'          # TCP 80 + 443
sudo ufw allow 60010:60030/udp       # main room + created rooms (match ROOM_PORT_MIN..MAX)
sudo ufw enable
sudo ufw status
```

If your provider has its own firewall (Hetzner Cloud Firewall, AWS Security Group, …), open the
**same ports there too**. The lobby's port 5310 stays closed: it only listens on `127.0.0.1`.

## 5. System user and folders

```bash
sudo useradd --system --create-home --home-dir /opt/the-room --shell /usr/sbin/nologin theroom
sudo mkdir -p /opt/the-room/{app,lobby,data,godot-app}
sudo chown -R theroom:theroom /opt/the-room
sudo chmod 750 /opt/the-room
```

Everything below that runs as `theroom` uses `sudo -u theroom bash -c '…'`. That works even though
the account has no login shell.

## 6. .NET 8 SDK (private copy)

```bash
sudo -u theroom bash -c '
  cd /opt/the-room &&
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh &&
  bash dotnet-install.sh --channel 8.0 --install-dir /opt/the-room/dotnet'
sudo -u theroom /opt/the-room/dotnet/dotnet --list-sdks       # expect 8.0.x
sudo -u theroom /opt/the-room/dotnet/dotnet --list-runtimes   # expect Microsoft.AspNetCore.App 8.0.x
```

The SDK includes the ASP.NET runtime the lobby needs. Installing into `/opt/the-room/dotnet` keeps it
separate from any other .NET on the machine.

## 7. Godot 4.7.2 (.NET / mono, Linux)

The version must **match the project exactly**: `Godot.NET.Sdk/4.7.2` in `The Room.csproj`.

```bash
sudo -u theroom bash -c '
  cd /opt/the-room &&
  curl -fL -o godot.zip https://github.com/godotengine/godot/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_linux_x86_64.zip &&
  unzip -q godot.zip -d godot-app && rm godot.zip'
GODOT=/opt/the-room/godot-app/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
sudo -u theroom "$GODOT" --headless --version       # expect 4.7.2.stable.mono...
```

If that download link has moved, get the "Linux x86_64, .NET" build for 4.7.2 from
<https://godotengine.org/download/archive/>.

## 8. The code

**Option A: from your dev machine** (what production uses). Add an SSH alias for the new server in
`~/.ssh/config` (root login), then run:

```bash
make deploy-server DEPLOY_HOST=<your-ssh-alias>
```

This rsyncs the repo, builds the game, publishes the lobby and installs the systemd unit. **Edit
the unit's values (step 9) before you run it**, or run it and fix them after. Then skip to step 10.

**Option B: on the server with git.**

```bash
sudo -u theroom git clone https://github.com/Voidra-iq/the-room.git /opt/the-room/app
sudo -u theroom bash -c '
  export PATH=/opt/the-room/dotnet:$PATH DOTNET_ROOT=/opt/the-room/dotnet
  cd /opt/the-room/app &&
  dotnet build "The Room.sln" &&
  dotnet publish services/lobby/Lobby.csproj -c Release -o /opt/the-room/lobby'
```

`dotnet build` compiles the game's C# into `.godot/mono`, which the headless room servers load.
Rebuild after every code update.

## 9. The lobby service (systemd)

```bash
sudo install -m 644 /opt/the-room/app/deploy/systemd/the-room-lobby.service /etc/systemd/system/
sudo systemctl edit --full the-room-lobby
```

Set these `Environment=` lines for your server:

| Line | Set to |
|---|---|
| `PUBLIC_HOST=` | `<UDP_HOST>`: the hostname players are told to connect to |
| `GODOT_BIN=` | the path from step 7, if different |
| `MAX_ROOMS=` | based on RAM (see [Rooms → Sizing](lobby/rooms.md#sizing)) |
| `ROOM_PORT_MIN=` / `ROOM_PORT_MAX=` | must match the firewall range from step 4 |
| `MAIN_ROOM_PORT=` | `60010` (or `0` for no permanent room) |
| `ROOM_IDLE_SECONDS=` | seconds before an empty created room closes |

All settings are described in [Lobby → Configuration](lobby/Intro.md#configuration). Then start it:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now the-room-lobby
systemctl status the-room-lobby --no-pager
curl -s http://127.0.0.1:5310/api/health            # {"status":"ok"}
curl -s http://127.0.0.1:5310/api/rooms             # MAIN, state "waiting" after a few seconds
ss -ulpn | grep 60010                               # the main room is listening
```

The unit runs the lobby as `theroom` with a read-only system (`ProtectSystem=strict`). It can only
write to `app`, `data` and `.local`. `KillMode=control-group` means stopping it also stops every
room.

## 10. nginx + HTTPS

On a new server the repo's nginx file can't be used as-is: it references the production
certificate. Start with this **HTTP-only** site and let certbot add TLS.

```bash
sudo tee /etc/nginx/sites-available/<API_HOST>.conf > /dev/null <<'EOF'
server {
    listen 80;
    listen [::]:80;
    server_name <API_HOST>;

    access_log /var/log/nginx/<API_HOST>.access.log;
    error_log  /var/log/nginx/<API_HOST>.error.log;

    location /api/ {
        proxy_pass http://127.0.0.1:5310;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        client_max_body_size 16k;
        add_header Cache-Control "no-store" always;
    }

    location = /season {
        proxy_pass http://127.0.0.1:5310/api/leaderboard?pageSize=50;
    }

    location / {
        default_type application/json;
        return 200 '{"service":"the-room-api"}';
    }
}
EOF
sudo ln -sf /etc/nginx/sites-available/<API_HOST>.conf /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d <API_HOST> -m <EMAIL> --agree-tos --redirect
```

Certbot adds the 443 block and the HTTP→HTTPS redirect, and sets up automatic renewal. Check it with
`sudo certbot renew --dry-run`.

If Cloudflare proxies `<API_HOST>`, certbot's HTTP challenge still works. Switch Cloudflare to
**Full (strict)** afterwards.

**Never add a `location` for `/internal`.** Those routes are for the rooms on this machine only.

## 11. Point the game at your server

Clients learn the UDP host from the lobby (`PUBLIC_HOST`). They only need to know the API URL:

- **Permanent:** change `DefaultApiBaseUrl` in `core/Net.cs` to `https://<API_HOST>`, then rebuild
  and export the client (`make export-client`).
- **Per launch:** `the-room.exe -- --api=https://<API_HOST>`.

## 12. Verify end to end

From your own machine:

```bash
curl -s https://<API_HOST>/api/health
curl -s https://<API_HOST>/api/rooms                     # MAIN on <UDP_HOST>:60010
curl -s -X POST https://<API_HOST>/api/rooms -H 'Content-Type: application/json' \
     -d '{"name":"Install check","mode":"duelpit","isPrivate":true}'   # note the port, e.g. 60011

# join both rooms with headless bots from the repo (proves UDP reaches the main AND created-room ports)
godot --headless --path . -- --connect=<UDP_HOST> --port=60010 --bot
godot --headless --path . -- --connect=<UDP_HOST> --port=60011 --bot

curl -s https://<API_HOST>/api/rooms                     # players: 1, state: playing
```

Then open the game with `--api=https://<API_HOST>` and create, join and leave a room from the menu.
After a full match (8 minutes), it appears under **Match history**.

## 13. Operations

| Task | Command |
|---|---|
| Status | `systemctl status the-room-lobby` |
| Logs (rooms prefixed `[CODE]`) | `journalctl -u the-room-lobby -f` |
| Restart (disconnects every room) | `sudo systemctl restart the-room-lobby` |
| Change a setting | `sudo systemctl edit --full the-room-lobby`, then `daemon-reload` + restart |
| Update code | Option A: `make deploy-server DEPLOY_HOST=…`. Option B: `git pull`, then the build/publish from step 8 as `theroom`, then restart. |
| Live rooms | `curl -s http://127.0.0.1:5310/api/rooms` |
| Memory per room | `ps -o rss=,args= -C Godot_v4.7.2-stable_mono_linux.x86_64` |

### Backups

All match history lives in `/opt/the-room/data/lobby.db`. SQLite's online backup is safe while the
lobby runs:

```bash
sudo -u theroom sqlite3 /opt/the-room/data/lobby.db ".backup '/opt/the-room/data/lobby-$(date +%F).db'"
```

Nightly, keeping 14 days (root crontab, `sudo crontab -e`):

```
15 4 * * * sudo -u theroom sqlite3 /opt/the-room/data/lobby.db ".backup '/opt/the-room/data/lobby-$(date +\%F).db'" && find /opt/the-room/data -name 'lobby-*.db' -mtime +14 -delete
```

Copy backups off the machine as well. To **restore**:

1. `sudo systemctl stop the-room-lobby`
2. copy the backup over `lobby.db` and delete any `lobby.db-wal` / `lobby.db-shm`
3. `sudo chown theroom:theroom /opt/the-room/data/lobby.db`
4. `sudo systemctl start the-room-lobby`

## 14. Troubleshooting

| Symptom | Likely cause → fix |
|---|---|
| Menu: "Couldn't reach the lobby" | The lobby is down (`systemctl status`), nginx isn't proxying (`curl -v https://<API_HOST>/api/health`), the certificate is missing, or Cloudflare SSL mode is wrong (use Full (strict)). The client may also have the wrong `--api`. |
| nginx 502 on `/api/` | The lobby isn't running or not on 127.0.0.1:5310. Check `journalctl -u the-room-lobby`. |
| Rooms list fine, but joining times out ("The room didn't answer") | UDP is blocked (ufw **and** provider firewall, range 60010–60030), or `<UDP_HOST>` is proxied (orange) or points elsewhere, or `PUBLIC_HOST` is wrong. |
| Create room → 500 "failed to start" | `GODOT_BIN` is wrong or not executable, or `GAME_PATH` is wrong. The journal shows the exact error. |
| Room stuck on "starting" | The room can't reach the lobby's loopback URL, or the game isn't built (`.godot/mono` missing: run `dotnet build` in `app/`). Room output in the journal says which. |
| Create room → 503 | `MAX_ROOMS` reached or no free port. Wait for idle rooms to close, or raise the limit and the port range together. |
| Main room keeps restarting | Another process holds UDP 60010, e.g. an old `the-room-server.service`. Check `ss -ulpn \| grep 60010`, then disable the old unit. |
| Old clients can't join after an update | A game protocol change (RPCs); players need the new build. |

## 15. Security checklist

- The lobby listens on **loopback only**; the public only reaches `/api/` through nginx.
- `/internal/*` isn't proxied, and each room authenticates with a random per-room token passed
  on its command line.
- Everything runs as `theroom` with `NoNewPrivileges`, `ProtectSystem=strict` and `PrivateTmp`.
- Room names are cleaned, room processes start without a shell, and room creation is
  rate-limited (3 per IP per minute) and capped (`MAX_ROOMS`).
  - Caveat: if the origin IP is public (it is, through `<UDP_HOST>`), someone can call the HTTP
    API directly and forge the IP header that the rate limit reads. `MAX_ROOMS` still bounds the
    damage. For a stricter setup, allow TCP 80/443 only from
    [Cloudflare's IP ranges](https://www.cloudflare.com/ips/).
- Only open the UDP range you configured.
- Never commit `<SERVER_IP>`, tokens or webhook URLs to the repo. It's public.

## Uninstall

```bash
sudo systemctl disable --now the-room-lobby
sudo rm /etc/systemd/system/the-room-lobby.service && sudo systemctl daemon-reload
sudo rm /etc/nginx/sites-enabled/<API_HOST>.conf /etc/nginx/sites-available/<API_HOST>.conf && sudo systemctl reload nginx
sudo certbot delete --cert-name <API_HOST>
sudo ufw delete allow 60010:60030/udp
sudo userdel -r theroom        # removes /opt/the-room, including lobby.db: back it up first
```
