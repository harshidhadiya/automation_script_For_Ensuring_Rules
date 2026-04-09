using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BugAuditScript.Models;

namespace BugAuditScript.HttpRequests;


public static class HttpCalls
{
    
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