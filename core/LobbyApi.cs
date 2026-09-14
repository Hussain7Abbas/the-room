using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace TheRoom.Core;

/// <summary>
/// Client side of the lobby's public API (services/lobby, room-api.iscoded.com/api). Used by the
/// main menu. Every call returns a result instead of throwing, with a message fit to show the
/// player. These records mirror services/lobby/Models.cs; the two deploy separately, so they're
/// duplicated rather than shared.
/// </summary>
public static class LobbyApi
{
    public sealed record RoomInfo(string Code, string Name, string Mode, bool IsPrivate, int Players, int MaxPlayers,
        string State, string Host, int Port, DateTime CreatedAt)
    {
        public bool IsFull => Players >= MaxPlayers;
    }

    public sealed record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

    public sealed record PlayerLine(int Rank, string Name, string Character, int Score, int Kills, int Deaths);

    public sealed record MatchSummary(long Id, string RoomCode, string RoomName, string Mode, DateTime EndedAt,
        double DurationSeconds, string? MvpName, int MvpScore, int PlayerCount, List<PlayerLine> TopPlayers);

    public sealed record MatchDetail(long Id, string RoomCode, string RoomName, string Mode, DateTime EndedAt,
        double DurationSeconds, string? MvpName, int MvpScore, List<PlayerLine> Players, List<string> Awards);

    public sealed record LeaderboardRow(int Rank, string Name, int Matches, int Wins, int Kills, int Deaths,
        int TotalScore, int BestScore);

    public sealed record Result<T>(T? Value, string? Error)
    {
        public bool Ok => Error is null;
    }

    private sealed record ErrorBody(string? Error);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Url(string path) => $"{Net.Instance.ApiBaseUrl}/api/{path}";

    public static Task<Result<List<RoomInfo>>> ListRooms() => Get<List<RoomInfo>>("rooms");

    public static Task<Result<RoomInfo>> GetRoom(string code) => Get<RoomInfo>($"rooms/{Uri.EscapeDataString(code.Trim().ToUpperInvariant())}");

    public static Task<Result<RoomInfo>> CreateRoom(string name, string mode, bool isPrivate) =>
        Send<RoomInfo>(HttpMethod.Post, "rooms", new { name, mode, isPrivate });

    public static Task<Result<PagedResult<MatchSummary>>> ListMatches(int page, int pageSize, string? player) =>
        Get<PagedResult<MatchSummary>>($"matches?page={page}&pageSize={pageSize}" +
            (string.IsNullOrWhiteSpace(player) ? "" : $"&player={Uri.EscapeDataString(player)}"));

    public static Task<Result<MatchDetail>> GetMatch(long id) => Get<MatchDetail>($"matches/{id}");

    public static Task<Result<PagedResult<LeaderboardRow>>> Leaderboard(int page, int pageSize) =>
        Get<PagedResult<LeaderboardRow>>($"leaderboard?page={page}&pageSize={pageSize}");

    private static Task<Result<T>> Get<T>(string path) => Send<T>(HttpMethod.Get, path, null);

    private static async Task<Result<T>> Send<T>(HttpMethod method, string path, object? body)
    {
        try
        {
            using var request = new HttpRequestMessage(method, Url(path));
            if (body is not null)
                request.Content = JsonContent.Create(body, options: Json);

            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return new Result<T>(await response.Content.ReadFromJsonAsync<T>(Json), null);

            string? message = null;
            try { message = (await response.Content.ReadFromJsonAsync<ErrorBody>(Json))?.Error; }
            catch (JsonException) { }
            return new Result<T>(default, message ?? $"The lobby answered HTTP {(int)response.StatusCode}.");
        }
        catch (TaskCanceledException)
        {
            return new Result<T>(default, "The lobby didn't answer in time.");
        }
        catch (HttpRequestException e)
        {
            return new Result<T>(default, $"Couldn't reach the lobby ({e.Message}).");
        }
    }
}
