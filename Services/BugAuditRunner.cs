using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BugAuditScript.Helpers;
using BugAuditScript.HttpRequests;
using BugAuditScript.Services;
using BUGAUDITSCRIPT.GoogleSheetUtility;
using DocumentFormat.OpenXml.Vml.Spreadsheet;
using Google.Apis.Logging;
using Microsoft.Extensions.Configuration;
namespace BugAuditScript.Services;

public class BugAuditRunner
{
    private readonly string _apiUrl;
    private readonly string _email;
    private readonly string _apiKey;
    private readonly string _commentUrl;
    private readonly string _csvFileFolder;
    private readonly Channel<string> csvChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly Channel<JsonElement> _issueChannel = Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(500));
    private readonly Channel<string> _responseChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(500));
    private readonly string _credentialPath;
    private string directory_of_csv=AppDomain.CurrentDomain.BaseDirectory;

    private readonly SemaphoreSlim _httpSemaphore = new(10);
    private readonly string _spreadsheetid;

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
        _credentialPath = config["AppSettings:credential_file_path"] ?? throw new InvalidOperationException("Missing Config : AppSettings:credential_file_path");
        _csvFileFolder= config["AppSettings:output"] ?? throw new InvalidOperationException("Missing Config : AppSettings:output");
        _spreadsheetid = config["AppSettings:spread_sheet_id"] ?? throw new InvalidOperationException("Missing Config : AppSettings:spread_sheet_id");

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

    public async Task RunAsync(string input, string? input1 = "", string? input2 = "", bool flag = false,bool flagPushInSheet=false)
    {
        var (jql, description) = JqlQueryBuilder.BuildQuery(input, input1, input2);
        var csvFileName = BuildCsvFileName(input, input1, input2);
        
        if(!string.IsNullOrEmpty(_csvFileFolder))
        {
            if(!PathUtility.DirectoryExistsStrict(_csvFileFolder))  throw new DirectoryNotFoundException($"{_csvFileFolder} in your configuration file not exist like in the example config.json ");
            directory_of_csv=_csvFileFolder;
        }

        csvFileName = Path.Combine(directory_of_csv, csvFileName);
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


        var writingResponseTask = Task.Run(async () =>
        {
            using var responseStreamWriter = new StreamWriter("response.json", append: true);
            await responseStreamWriter.WriteAsync(string.Empty);
            await foreach (var row in _responseChannel.Reader.ReadAllAsync())
            {
                await responseStreamWriter.WriteLineAsync(row);
                await responseStreamWriter.FlushAsync();
            }
        });

        await FetchAndProcessAllPages(jql, description);


        _issueChannel.Writer.Complete();
        await Task.WhenAll(workers);

        csvChannel.Writer.Complete();
        await writingTask;
        _responseChannel.Writer.Complete();
        await writingResponseTask;


        Console.WriteLine("datas of from the services");
        if(flagPushInSheet)
        await GoogleSheet.UpsertToGoogleSheet(csvFileName, _credentialPath,_spreadsheetid);
        if (flag)
            DeleteCsvFile(csvFileName);
        Helper.Log($"\n✅ Done. Report saved to: {csvFileName}");
        Console.WriteLine($"\n✅ Done. Report saved to: {csvFileName}");
    }



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

            var rawJson = await HttpCalls.GetAsync(_apiUrl, _email, _apiKey, jql, token);
            await _responseChannel.Writer.WriteAsync(Helper.PrettyPrint(rawJson));
            using var doc = JsonDocument.Parse(rawJson);

            var issues = doc.RootElement.GetProperty("issues");
            await _issueChannel.Writer.WriteAsync(issues.Clone());


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
        var semaphores = new SemaphoreSlim(5);
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

        await csvChannel.Writer.WriteAsync(BuildCsvRow(issueKey, CollectMissingFields(fields, hasRootcause, hasFix, hasImpact), fields, hasRootcause, hasFix, hasImpact));
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
                if (key.Contains("BPA"))
                    return;
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


    private static List<string> CollectMissingFields(JsonElement fields, bool hasRoot, bool hasFix, bool hasImpact)
    {
        bool switchingFlag = true;
        var missing = new List<string>();

        var rootCauseCount = fields.TryGetProperty("customfield_12608", out var rc) &&
                             rc.ValueKind == JsonValueKind.Array
            ? rc.EnumerateArray().Count()
            : 0;
        if (rootCauseCount == 0 && !hasRoot)
        {
            missing.Add("Root Cause [ In Fields | In Comments ]");
            switchingFlag = false;
        }
        if (rootCauseCount == 0 && switchingFlag)
            missing.Add("Root Cause ( In Field )");

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
        if (!hasRoot && switchingFlag)
            missing.Add("Root Cause ( In Comments )");
        if (!hasFix)
            missing.Add("Applied Fix ( In Comments )");
        if (!hasImpact)
            missing.Add("Impact Details ( In comments )");

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

        return string.Join(" | ", missing);
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

        if (rc.ValueKind == JsonValueKind.Null)
        {
            rootCause = string.Empty;
        }
        return string.Join(",",
            Helper.MakeJiraLink(key),
            Helper.Escape(status, true),
            Helper.Escape(missingText, true),
            Helper.Escape(rootCause),
            Helper.Escape(fixCount.ToString()),
            Helper.Escape(pr),
            Helper.Escape(TimeHelper.Now().ToString("yyyy-MM-dd HH:mm:ss"), true),
            Helper.Escape(hasRootCause.ToString()),
            Helper.Escape(hasFix.ToString()),
            Helper.Escape(hasImpactDetails.ToString())
        );
    }

    private static string BuildCsvFileName(string input, string startDate = "", string EndDate = "")
    {
        var label = JqlQueryBuilder.GetFileLabel(input, startDate, EndDate);
        return $"BugReport_{TimeHelper.Now():yyyy_MM_dd}_Time_{TimeHelper.Now():HH_mm}_{label}.csv";
    }

    private static void InitialiseCsvFile(string fileName)
        => File.WriteAllText(fileName, String.Join(",", Helper.fields));
    private static void DeleteCsvFile(string fileName) => File.Delete(fileName);
    private static void PrintSectionHeader(string description)
    {
        Console.WriteLine();
        Console.WriteLine(description);
        Console.WriteLine(new string('-', 40));
    }
}
