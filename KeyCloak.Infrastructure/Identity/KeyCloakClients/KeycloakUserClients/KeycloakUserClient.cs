using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace KeyCloak.Infrastructure.Identity.KeyCloakClients.KeycloakUserClients;

public sealed class KeycloakUserClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<bool> ForgotPasswordAsync(string email, string adminUrl, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync($"{adminUrl}/users?email={email}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(cancellationToken: cancellationToken);
        if (users == null || users.Count == 0) return false;

        var userId = users[0]["id"].ToString();
        var actionResponse = await httpClient.PutAsJsonAsync($"{adminUrl}/users/{userId}/execute-actions-email", new[] { "UPDATE_PASSWORD" }, cancellationToken);
        return actionResponse.IsSuccessStatusCode;
    }

    public async Task<bool> ResendConfirmationEmailAsync(string email, string adminUrl, CancellationToken cancellationToken)
    {
        string? accessToken = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync($"{adminUrl}/users?email={email}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(cancellationToken: cancellationToken);
        if (users == null || users.Count == 0) return false;

        var userId = users[0]["id"].ToString();
        var actionResponse = await httpClient.PutAsJsonAsync($"{adminUrl}/users/{userId}/execute-actions-email", new[] { "VERIFY_EMAIL" }, cancellationToken);
        return actionResponse.IsSuccessStatusCode;
    }
}

