using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BugAuditScript.Models;

namespace BugAuditScript.HttpRequests;

/// <summary>
/// Handles all HTTP communication with the Jira REST API.
/// Credentials are passed per-call so this class stays stateless and testable.
/// </summary>
public static class HttpCalls
{
    // ─── GET ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an authenticated GET request and returns the raw JSON response body.
    /// </summary>
    /// <param name="baseUrl">The Jira API base URL (from config).</param>
    /// <param name="email">Jira account email used for Basic Auth.</param>
    /// <param name="apiKey">Jira API token used for Basic Auth.</param>
    /// <param name="queryString">
    /// Optional query string (e.g. "jql=...&amp;maxResults=100") appended to the URL.
    /// </param>
    public static async Task<string> GetAsync(
        string baseUrl,
        string email,
        string apiKey,
        string queryString = "")
    {
        using var httpClient = CreateClient(email, apiKey);

        var url = string.IsNullOrEmpty(queryString)
            ? baseUrl
            : $"{baseUrl}?{queryString}";

        Console.WriteLine($"[HTTP GET] {url}");

        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"GET failed for URL: {url}\n{ex.Message}", ex);
        }
    }

    // ─── POST ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an authenticated POST request with a <see cref="JiraSearchRequest"/>
    /// body and returns the raw JSON response body.
    /// </summary>
    /// <param name="url">Full endpoint URL.</param>
    /// <param name="email">Jira account email used for Basic Auth.</param>
    /// <param name="apiKey">Jira API token used for Basic Auth.</param>
    /// <param name="request">The search request payload to serialize as JSON.</param>
    public static async Task<string> PostAsync(
        string url,
        string email,
        string apiKey,
        JiraSearchRequest request)
    {
        using var httpClient = CreateClient(email, apiKey);

        Console.WriteLine($"[HTTP POST] {url}");

        try
        {
            var json    = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"POST failed for URL: {url}\n{ex.Message}", ex);
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a pre-configured <see cref="HttpClient"/> with Basic Auth headers.
    /// </summary>
    private static HttpClient CreateClient(string email, string apiKey)
    {
        var client = new HttpClient();
        var token  = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiKey}"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", token);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}