using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GunZLeague.Models.ViewModels;

namespace GunZLeague.Services
{
    public class TwitchStreamStatusService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TwitchStreamStatusService> _logger;
        private string? _accessToken;
        private DateTimeOffset _accessTokenExpiresAt;

        public TwitchStreamStatusService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<TwitchStreamStatusService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<PartnerStreamStatusViewModel>> GetStreamsAsync(CancellationToken cancellationToken = default)
        {
            var streams = GetConfiguredStreams();
            if (streams.Count == 0)
            {
                return streams;
            }

            var clientId = _configuration["Twitch:ClientId"];
            var clientSecret = _configuration["Twitch:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                foreach (var stream in streams)
                {
                    stream.StatusUnavailable = true;
                }

                return streams;
            }

            try
            {
                var token = await GetAppAccessTokenAsync(clientId, clientSecret, cancellationToken);
                var query = string.Join("&", streams.Select(s => $"user_login={Uri.EscapeDataString(s.Channel)}"));
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/streams?{query}");
                request.Headers.Add("Client-Id", clientId);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<TwitchStreamsResponse>(stream, cancellationToken: cancellationToken);
                var liveByLogin = new Dictionary<string, TwitchStream>(StringComparer.OrdinalIgnoreCase);
                foreach (var liveStream in payload?.Data ?? new List<TwitchStream>())
                {
                    if (!string.IsNullOrWhiteSpace(liveStream.UserLogin))
                    {
                        liveByLogin[liveStream.UserLogin] = liveStream;
                    }
                }

                foreach (var configuredStream in streams)
                {
                    if (!liveByLogin.TryGetValue(configuredStream.Channel, out var liveStream))
                    {
                        continue;
                    }

                    configuredStream.IsOnline = true;
                    configuredStream.DisplayName = string.IsNullOrWhiteSpace(liveStream.UserName)
                        ? configuredStream.DisplayName
                        : liveStream.UserName;
                    configuredStream.Title = liveStream.Title;
                    configuredStream.GameName = liveStream.GameName;
                    configuredStream.ViewerCount = liveStream.ViewerCount;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not load Twitch stream status.");
                foreach (var stream in streams)
                {
                    stream.StatusUnavailable = true;
                }
            }

            return streams;
        }

        private List<PartnerStreamStatusViewModel> GetConfiguredStreams()
        {
            var links = _configuration.GetSection("Community:Streams").Get<string[]>() ?? Array.Empty<string>();
            return links
                .Select(CreateStream)
                .Where(stream => stream != null)
                .Select(stream => stream!)
                .ToList();
        }

        private static PartnerStreamStatusViewModel? CreateStream(string streamLink)
        {
            if (!Uri.TryCreate(streamLink, UriKind.Absolute, out var streamUri))
            {
                return null;
            }

            var channel = streamUri.Segments.LastOrDefault()?.Trim('/');
            if (string.IsNullOrWhiteSpace(channel))
            {
                return null;
            }

            return new PartnerStreamStatusViewModel
            {
                DisplayName = channel,
                Channel = channel.ToLowerInvariant(),
                Url = streamUri.ToString()
            };
        }

        private async Task<string> GetAppAccessTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return _accessToken;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["grant_type"] = "client_credentials"
                })
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var token = await JsonSerializer.DeserializeAsync<TwitchTokenResponse>(stream, cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                throw new HttpRequestException("Twitch token response did not include an access token.");
            }

            _accessToken = token.AccessToken;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn));
            return _accessToken;
        }

        private sealed class TwitchTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }

        private sealed class TwitchStreamsResponse
        {
            [JsonPropertyName("data")]
            public List<TwitchStream>? Data { get; set; }
        }

        private sealed class TwitchStream
        {
            [JsonPropertyName("user_login")]
            public string? UserLogin { get; set; }

            [JsonPropertyName("user_name")]
            public string? UserName { get; set; }

            [JsonPropertyName("game_name")]
            public string? GameName { get; set; }

            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("viewer_count")]
            public int ViewerCount { get; set; }
        }
    }
}
