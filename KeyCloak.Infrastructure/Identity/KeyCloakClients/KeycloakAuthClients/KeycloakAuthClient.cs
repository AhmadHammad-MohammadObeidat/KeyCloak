using KeyCloak.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakAuthClients;

public sealed class KeycloakAuthClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TokenResponse> UserLoginAsync(string username, string password, string publicClientId, string scope,
                                                    string grantType, string tokenUrl, CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", publicClientId),
            new KeyValuePair<string, string>("scope", scope),
            new KeyValuePair<string, string>("grant_type", grantType),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(tokenUrl)) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>() ?? new TokenResponse();
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, string publicClientId, string grantType, string tokenUrl, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", publicClientId),
            new KeyValuePair<string, string>("grant_type", grantType),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(tokenUrl)) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>() ?? new TokenResponse();
    }

    public async Task<string> GetAdminAccessTokenAsync(string tokenUrl, string clientId, string clientSecret, string username, string password, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        return tokenResponse?.AccessToken ?? throw new InvalidOperationException("Failed to get access token");
    }

    public async Task<string> RegisterUserAsync(UserRepresentation user, CancellationToken cancellationToken = default)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.PostAsJsonAsync("users", user, cancellationToken);
        response.EnsureSuccessStatusCode();
        return ExtractUserIdFromLocation(response);
    }

    private static string ExtractUserIdFromLocation(HttpResponseMessage response)
    {
        const string segment = "users/";
        string? location = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Location header is missing");

        int index = location.IndexOf(segment, StringComparison.InvariantCultureIgnoreCase);
        return location[(index + segment.Length)..];
    }
}
