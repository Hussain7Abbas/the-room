using TheRoom.Lobby;

// The Room lobby: creates and lists rooms, and stores match history + the leaderboard.
// Public routes live under /api (nginx proxies room-api.iscoded.com/api/ here). /internal is
// for the rooms' own game servers on this machine and is never proxied.

var settings = LobbySettings.FromEnvironment();
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(settings.ListenUrl);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<MatchStore>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RoomManager>());

var app = builder.Build();

static string ClientIp(HttpContext ctx) =>
    ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
    ?? ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
    ?? ctx.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";

static IResult Error(int status, string message) => Results.Json(new ErrorBody(message), statusCode: status);

// ---- Public ----

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/rooms", (RoomManager rooms) => Results.Ok(rooms.ListPublic()));

app.MapGet("/api/rooms/{code}", (string code, RoomManager rooms) =>
    rooms.Get(code) is { } room ? Results.Ok(room) : Error(404, $"No room with code {code.ToUpperInvariant()}."));

app.MapPost("/api/rooms", (CreateRoomRequest request, HttpContext ctx, RoomManager rooms) =>
{
    var result = rooms.Create(request, ClientIp(ctx));
    return result.Room is { } room ? Results.Created($"/api/rooms/{room.Code}", room) : Error(result.Status, result.Error!);
});

app.MapGet("/api/matches", (int? page, int? pageSize, string? player, MatchStore store) =>
    Results.Ok(store.ListMatches(page, pageSize, player)));

app.MapGet("/api/matches/{id:long}", (long id, MatchStore store) =>
    store.GetMatch(id) is { } match ? Results.Ok(match) : Error(404, "Match not found."));

app.MapGet("/api/leaderboard", (int? page, int? pageSize, MatchStore store) =>
    Results.Ok(store.Leaderboard(page, pageSize)));

// ---- Internal (room game servers only) ----

app.MapPost("/internal/rooms/{code}/heartbeat", (string code, HeartbeatRequest heartbeat, RoomManager rooms) =>
    rooms.Heartbeat(code, heartbeat) ? Results.NoContent() : Results.Unauthorized());

app.MapPost("/internal/rooms/{code}/matches", (string code, MatchReport report, RoomManager rooms, MatchStore store, ILogger<Program> log) =>
{
    var room = rooms.Authorize(code, report.Token);
    if (room is null)
        return Results.Unauthorized();
    if (report.Players is not { Count: > 0 and <= 32 })
        return Error(400, "A match needs 1–32 players.");

    var id = store.Insert(room.Code, room.Name, room.Mode, report);
    log.LogInformation("Recorded match {Id} from room {Code} ({Players} players).", id, room.Code, report.Players.Count);
    return Results.Created($"/api/matches/{id}", new { id });
});

app.Run();

public partial class Program;
