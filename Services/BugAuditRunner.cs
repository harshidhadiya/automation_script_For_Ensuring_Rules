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
    private string _commentUrl;
    private readonly Channel<string> csvChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait
    });


    public BugAuditRunner(IConfiguration config)
    {
        _apiUrl = config["AppSettings:issues_url"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:issues_url");
        _email = config["AppSettings:api_email"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:api_email");
        _apiKey = config["AppSettings:api_key"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:api_key");
        _commentUrl = config["AppSettings:comment_url"] ?? throw new InvalidOperationException("Missing config: AppSetting:comment_url");
    }

    public async Task RunAsync(string input)
    {
        var (jql, description) = JqlQueryBuilder.BuildQuery(input);
        var csvFileName = BuildCsvFileName(input);

        File.WriteAllText("response.json", string.Empty);

        InitialiseCsvFile(csvFileName);

        Console.WriteLine($"\n>> {description}");
        Console.WriteLine($">> Output: {csvFileName}\n");

        await FetchAndProcessAllPages(jql, description, csvFileName);
        Console.WriteLine($"\n✅ Done. Report saved to: {csvFileName}");
        csvChannel.Writer.Complete();

    }


    private async Task FetchAndProcessAllPages(
        string jql,
        string description,
        string csvFileName)
    {
        int currentStart = 0;
        bool hasMorePages = true;
        var writingTask = Task.Run(async () =>
        {

            using var csvStreamWriter = new StreamWriter(csvFileName, append: true);

            await foreach (var row in csvChannel.Reader.ReadAllAsync())
            {
                await csvStreamWriter.WriteLineAsync(row);
            }
        });


        while (hasMorePages)
        {
            var rawJson = await HttpCalls.GetAsync(_apiUrl, _email, _apiKey, jql);

            // File.AppendAllText("response.json", Helper.PrettyPrint(rawJson));

            Console.WriteLine("Page fetched successfully.");

            using var doc = JsonDocument.Parse(rawJson);
            var issues = doc.RootElement.GetProperty("issues");

            hasMorePages = doc.RootElement.TryGetProperty("isLast", out var isLast)
                           && !isLast.GetBoolean();

            if (hasMorePages)
            {
                jql = JqlQueryBuilder.AdvancePage(jql, currentStart);
                currentStart += 1000;
                Console.WriteLine($"Fetching next page (startAt={currentStart})…");
            }

            PrintSectionHeader(description);

            // var csv =await this.ProcessPage(issues);
            await this.ProcessPage(issues);
            // File.AppendAllText(csvFileName, csv);
        }
    }




    private async Task FetchAllComments(string issueKey, int totals, bool flag, JsonElement fields)
    {


        var (hasRootcause1, hasFix1, hasImpact1) = (false, false, false);
        totals = (int)Math.Ceiling((decimal)totals / 20);
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var readTask = Task.Run(async () =>
        {


            await foreach (var row in channel.Reader.ReadAllAsync())
            {
                var data = row;
                if (data != null)
                {
                    using var datas = JsonDocument.Parse(data);
                    var (hasRootCause, hasFix, hasImpact) = JiraCommentHelper.CheckComments2(datas, flag);
                    if (!hasRootcause1 && hasRootCause)
                        hasRootcause1 = true;
                    if (!hasFix1 && hasFix)
                        hasFix1 = true;
                    if (!hasImpact1 && hasImpact)
                        hasImpact1 = true;
                    if (hasRootcause1 && hasFix1 && hasImpact1)
                    {

                        cancellation.Cancel();
                    }
                }
            }
        }, token);
        var current = _commentUrl.Replace("{issueKey}", issueKey);
        var producer = Enumerable.Range(0, totals).Select(async i =>
        {
            var semaphores = new SemaphoreSlim(5);
            var query = JqlQueryBuilder.getStringCorrect(i * 20);
            try
            {
                var result = await HttpCalls.GetAsync(current, _email, _apiKey, query);
                await channel.Writer.WriteAsync(result);
            }
            finally
            {
                semaphores.Release();
            }
        });

        await Task.WhenAll(producer);
        channel.Writer.Complete();
        await readTask;
        await csvChannel.Writer.WriteAsync(BuildCsvRow(issueKey, CollectMissingFields(fields), fields, hasRootcause1, hasFix1, hasImpact1));

    }

    private async Task<string> ProcessPage(JsonElement issues)
    {
        var csv = new StringBuilder();
        var taskList = new List<Task>();
        foreach (var bug in issues.EnumerateArray())
        {
            var work = Task.Run(async () =>
            {


                var key = bug.GetProperty("key").GetString()!;
                var fields = bug.GetProperty("fields");

                var environment = GetEnvironment(fields);

                var totals = 0;
                if (fields.TryGetProperty("comment", out var commentField))
                {
                    if (commentField.TryGetProperty("total", out var total))
                    {
                        totals = int.Parse(total.ToString());
                    }
                }
                var flag = true;
                if (fields.TryGetProperty("priority", out var criticality))
                {
                    if (criticality.TryGetProperty("name", out var value))
                    {
                        if (!string.IsNullOrEmpty(value.ToString()) && value.ToString().Equals("Critical", StringComparison.OrdinalIgnoreCase))
                            flag = true;

                        else flag = false;
                        Console.WriteLine(value.ToString() + "Here is the priority value from the jira related to current bug" + flag.ToString());
                    }
                }


                await this.FetchAllComments(key, totals, flag, fields);


                // var (hasRootCause, hasFix, hasImpactDetails) = JiraCommentHelper.CheckComments(commentField, flag);

                // Console.WriteLine($"  Bug {key} → RootCauseInComment:{hasRootCause}  FixInComment:{hasFix}  ImpactInComment:{hasImpactDetails}");

                // var missing = CollectMissingFields(fields);

                // PrintBugSummary(key, missing);

                // var missingText = BuildMissingText(missing, hasRootCause, hasFix,hasImpactDetails);

                // if (missingText == "None" && hasRootCause && hasFix)
                // {
                // csv.AppendLine(BuildCsvRow(key, missing, fields, hasRootCause, hasFix, hasImpactDetails));
                // }

            });
            taskList.Add(work);
        }
        await Task.WhenAll(taskList);
        return csv.ToString();
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

        var rootCause = fields.TryGetProperty("customfield_12608", out var rc) &&
                 rc.ValueKind == JsonValueKind.Array
     ? rc.EnumerateArray().Count()
     : 0;
        if (rootCause == 0)
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

        var text = string.Join(" | ", missing)
            .Replace("Root Cause", "Root Cause(In Fields)");
        return text;
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

    private static void PrintBugSummary(string key, List<string> missing)
    {
        Console.Write("=> ");
        if (missing.Any())
        {
            Console.WriteLine($"Bug {key}:");
            foreach (var m in missing)
                Console.WriteLine($"   - Missing {m}");
        }
        else
        {
            Console.WriteLine($"Bug {key} → ✅ All Good");
        }
        Console.WriteLine(new string('-', 40));
    }
}
