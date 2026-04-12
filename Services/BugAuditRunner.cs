using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BugAuditScript.Helpers;
using BugAuditScript.HttpRequests;
using BugAuditScript.Services;
using Microsoft.Extensions.Configuration;

namespace BugAuditScript.Services;

public class BugAuditRunner
{
    private readonly string _apiUrl;
    private readonly string _email;
    private readonly string _apiKey;
    private readonly string _commentUrl;
    private readonly Channel<string> csvChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly Channel<JsonElement> _issueChannel = Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(500));


    // Global semaphore to limit concurrent HTTP requests to Jira API
    private readonly SemaphoreSlim _httpSemaphore = new(10);

    public BugAuditRunner(IConfiguration config)
    {
        _apiUrl = config["AppSettings:issues_url"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:issues_url");
        _email = config["AppSettings:api_email"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:api_email");
        _apiKey = config["AppSettings:api_key"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:api_key");
        _commentUrl = config["AppSettings:comment_url"]
            ?? throw new InvalidOperationException("Missing config: AppSetting:comment_url");
    }
     
    private List<Task> StartWorkers(int workerCount)
    {
        return Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            await foreach (var issue in _issueChannel.Reader.ReadAllAsync())
            {
                Console.WriteLine("entered here in the startworkers");
                await ProcessPage(issue);
            }
        })).ToList();
    }

    public async Task RunAsync(string input)
    {
        var (jql, description) = JqlQueryBuilder.BuildQuery(input);
        var csvFileName = BuildCsvFileName(input);
        var workers = StartWorkers(5);
        File.WriteAllText("response.json", string.Empty);
        InitialiseCsvFile(csvFileName);

        Console.WriteLine($"\n>> {description}");
        Console.WriteLine($">> Output: {csvFileName}\n");

        var writingTask = Task.Run(async () =>
        {
            using var csvStreamWriter = new StreamWriter(csvFileName, append: true);
            await foreach (var row in csvChannel.Reader.ReadAllAsync())
            {
                await csvStreamWriter.WriteLineAsync(row);
                await csvStreamWriter.FlushAsync();
            }
        });

        await FetchAndProcessAllPages(jql, description);
        

        _issueChannel.Writer.Complete();        
        await Task.WhenAll(workers);            

        csvChannel.Writer.Complete();           
        await writingTask;                      
        Console.WriteLine($"\n✅ Done. Report saved to: {csvFileName}");
    }

    // private async Task FetchAndProcessAllPages(string jql, string description)
    // {
    //     int currentStart = 0;
    //     bool hasMorePages = true;

    //     while (hasMorePages)
    //     {
    //         // Throttling page fetches as well
    //         await _httpSemaphore.WaitAsync();
    //         string rawJson;
    //         try
    //         {
    //             rawJson = await HttpCalls.GetAsync(_apiUrl, _email, _apiKey, jql);
    //         }
    //         finally
    //         {
    //             _httpSemaphore.Release();
    //         }

    //         Console.WriteLine("Page fetched successfully.");

    //         using var doc = JsonDocument.Parse(rawJson);
    //         var issues = doc.RootElement.GetProperty("issues");

    //         hasMorePages = doc.RootElement.TryGetProperty("isLast", out var isLast)
    //                        && !isLast.GetBoolean();

    //         if (hasMorePages)
    //         {
    //             jql = JqlQueryBuilder.AdvancePage(jql, currentStart);
    //             currentStart += issues.GetArrayLength();
    //             Console.WriteLine($"Fetching next page (startAt={currentStart})…");
    //         }

    //         PrintSectionHeader(description);
    //             await _issueChannel.Writer.WriteAsync(issues.Clone());
    //     }
    // }


private async Task FetchAndProcessAllPages(string jql, string description)
{
    string? nextPageToken = null;
bool hasMorePages = true;

while (hasMorePages)
{
    string token = "";

    if (!string.IsNullOrEmpty(nextPageToken))
    {
        token = nextPageToken;
    }

    var rawJson = await HttpCalls.GetAsync(_apiUrl, _email, _apiKey,jql,token);

    using var doc = JsonDocument.Parse(rawJson);

    var issues = doc.RootElement.GetProperty("issues");
    await _issueChannel.Writer.WriteAsync(issues.Clone());

    // ✅ GET TOKEN
    if (doc.RootElement.TryGetProperty("nextPageToken", out var tokenElement))
    {
        nextPageToken = tokenElement.GetString();
        hasMorePages = !string.IsNullOrEmpty(nextPageToken);
    }
    else
    {
        hasMorePages = false;
    }
}
}
    private async Task FetchAllComments(string issueKey, int totals, bool isCritical, JsonElement fields)
    {
        var (hasRootcause, hasFix, hasImpact) = (false, false, false);
        int totalPages = (int)Math.Ceiling((decimal)totals / 20);

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var readTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var row in channel.Reader.ReadAllAsync(token))
                {
                    using var doc = JsonDocument.Parse(row);
                    var (r, f, i) = JiraCommentHelper.CheckComments2(doc, isCritical);
                    
                    if (r) hasRootcause = true;
                    if (f) hasFix = true;
                    if (i) hasImpact = true;

                    if (hasRootcause && hasFix && hasImpact)
                    {
                        cts.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Processing comments for {issueKey}: {ex.Message}");
            }
        });

        var currentUrl = _commentUrl.Replace("{issueKey}", issueKey);
        var semaphores=new SemaphoreSlim(5);
        var producerTasks = Enumerable.Range(0, totalPages).Select(async i =>
        {
            if (token.IsCancellationRequested) return;

            await semaphores.WaitAsync(token);
            try
            {
                var query = JqlQueryBuilder.getStringCorrect(i * 20);
                var result = await HttpCalls.GetAsync(currentUrl, _email, _apiKey, query);
                await channel.Writer.WriteAsync(result, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Fetching comments for {issueKey} (Page {i}): {ex.Message}");
            }
            finally
            {
                semaphores.Release();
            }
        });

        await Task.WhenAll(producerTasks);
        channel.Writer.Complete();
        await readTask;

        await csvChannel.Writer.WriteAsync(BuildCsvRow(issueKey, CollectMissingFields(fields), fields, hasRootcause, hasFix, hasImpact));
    }

    private async Task ProcessPage(JsonElement issues)
    {

        var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
        await Parallel.ForEachAsync(issues.EnumerateArray(), options, async (bug, ct) =>
        {
            try
            {
                var key = bug.GetProperty("key").GetString()!;
                var fields = bug.GetProperty("fields");

                int totalComments = 0;
                if (fields.TryGetProperty("comment", out var commentField) &&
                    commentField.TryGetProperty("total", out var total))
                {
                    totalComments = total.GetInt32();
                }

                bool isCritical = false;
                if (fields.TryGetProperty("priority", out var priority) &&
                    priority.TryGetProperty("name", out var priorityName))
                {
                    isCritical = string.Equals(priorityName.GetString(), "Critical", StringComparison.OrdinalIgnoreCase);
                }

                await FetchAllComments(key, totalComments, isCritical, fields);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Processing bug: {ex.Message}");
            }
        });
    }

    private static string GetEnvironment(JsonElement fields)
    {
        if (fields.TryGetProperty("customfield_11001", out var env)
            && env.ValueKind == JsonValueKind.Object
            && env.TryGetProperty("value", out var val))
        {
            return val.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static List<string> CollectMissingFields(JsonElement fields)
    {
        var missing = new List<string>();

        var rootCauseCount = fields.TryGetProperty("customfield_12608", out var rc) &&
                             rc.ValueKind == JsonValueKind.Array
            ? rc.EnumerateArray().Count()
            : 0;
        if (rootCauseCount == 0)
            missing.Add("Root Cause");

        var fixCount = fields.TryGetProperty("fixVersions", out var fx)
            ? fx.EnumerateArray().Count()
            : 0;
        if (fixCount == 0)
            missing.Add("Fix Version");

        var pr = fields.TryGetProperty("customfield_11900", out var prEl)
            ? prEl.ToString()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(pr) || pr.Equals("{}", StringComparison.OrdinalIgnoreCase))
            missing.Add("Commits/PR");

        return missing;
    }

    private static string BuildMissingText(
        List<string> missing,
        bool hasRootCause,
        bool hasFix,
        bool hasImpactDetails)
    {
        if (!missing.Any())
        {
            if (!hasRootCause) return "Root Cause(In Comments)";
            if (!hasFix) return "Fix(In Comments)";
            if (!hasImpactDetails) return "Impact Details(In Comments)";
            return "None";
        }

        return string.Join(" | ", missing).Replace("Root Cause", "Root Cause(In Fields)");
    }

    private static string BuildCsvRow(
        string key,
        List<string> missing,
        JsonElement fields,
        bool hasRootCause,
        bool hasFix,
        bool hasImpactDetails)
    {
        var status = missing.Any() ? "🔴 Missing" : "✅ All Good";
        var missingText = BuildMissingText(missing, hasRootCause, hasFix, hasImpactDetails);

        var rootCause = fields.TryGetProperty("customfield_12608", out var rc)
            ? rc.ToString() : string.Empty;
        var fixCount = fields.TryGetProperty("fixVersions", out var fx)
            ? fx.EnumerateArray().Count() : 0;
        var pr = fields.TryGetProperty("customfield_11900", out var prEl)
            ? prEl.ToString() : string.Empty;

        return string.Join(",",
            Helper.Escape(key),
            Helper.Escape(status),
            Helper.Escape(missingText),
            Helper.Escape(rootCause),
            Helper.Escape(fixCount.ToString()),
            Helper.Escape(pr),
            Helper.Escape(TimeHelper.Now().ToString("yyyy-MM-dd HH:mm:ss")),
            Helper.Escape(hasRootCause.ToString()),
            Helper.Escape(hasFix.ToString()),
            Helper.Escape(hasImpactDetails.ToString())
        );
    }

    private static string BuildCsvFileName(string input)
    {
        var label = JqlQueryBuilder.GetFileLabel(input);
        return $"BugReport_{TimeHelper.Now():yyyy_MM_dd}_Time_{TimeHelper.Now():HH_mm}_{label}.csv";
    }

    private static void InitialiseCsvFile(string fileName)
        => File.WriteAllText(fileName,
            "BugId,Status,MissingFields,RootCause,Fix_Versions,Commits/PR," +
            "GeneratedAtIST,Has_root_cause_in_comments,Has_fix_in_comments,Has_impact_details_in_comments\n");

    private static void PrintSectionHeader(string description)
    {
        Console.WriteLine();
        Console.WriteLine(description);
        Console.WriteLine(new string('-', 40));
    }
}
