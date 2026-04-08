using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
public class JiraSearchRequest
{
    public string jql { get; set; } = "";
    public int maxResults { get; set; }
    public int startAt { get; set; }
    public List<string> fields { get; set; } = new();
    public bool fieldsByKeys { get; set; }
    public List<string> expand { get; set; } = new();
    public string? nextPageToken { get; set; }
}
public static class HttpCalls
{
    public static async Task<string> GetAsync(string url, string email, string apiKey, string query = "")
    {
        using (var httpClient = new HttpClient())
        {
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiKey}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                if (!string.IsNullOrEmpty(query))
                {
                    url = $"{url}?{query}";
                    Console.WriteLine($"Constructed URL: {url}");
                }
                // url="https://ahirharshidj.atlassian.net/rest/api/3/search/jql?jql=project=GD%20AND%20issuetype=Bug&fields=summary,status,description,created,updated&maxResults=100";
                // url="https://api.atlassian.com/ex/jira/646d538a-8a21-4040-9a5b-c589346aef1f/rest/api/3/search/jql?jql=project=GD&maxResults=10&fields=summary,status,description,comment,Assignee,reporter,created,updated&expand=renderedFields";
                // url="https://api.atlassian.com/ex/jira/1e7f3193-c0ee-46ad-9522-d5e86521fa04/rest/api/3/search/jql?jql=issuetype=Bug&fields=summary,status,description,comment,Assignee,reporter,created,updated&expand=renderedFields";
                // url="https://api.atlassian.com/ex/jira/1e7f3193-c0ee-46ad-9522-d5e86521fa04/rest/api/3/field";
                // url="https://api.atlassian.com/ex/jira/1e7f3193-c0ee-46ad-9522-d5e86521fa04/rest/api/3/search/jql?jql=issuetype=Bug&fields=fixVersions,customfield_11001,customfield_12608,customfield_11900&maxResults=10&startAt=0";
                Console.WriteLine($"Final URL: {url}");
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error fetching data from {url}: {ex.Message}", ex);
            }
        }
    }
    public static async Task<string> PostAsync(string url, string email, string apiKey, string jsonContent)
    {
        using (var httpClient = new HttpClient())
        {
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{email}:{apiKey}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                var body = new JiraSearchRequest
                {
                    jql = "project IS NOT EMPTY order by created DESC",
                    maxResults = 50
                };

                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);
                Console.WriteLine(response);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error posting data to {url}: {ex.Message}", ex);
            }
        }
    }
}