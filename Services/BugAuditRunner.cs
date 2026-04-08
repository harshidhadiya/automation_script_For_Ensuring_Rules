using System.Text;
using System.Text.Json;
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


    public BugAuditRunner(IConfiguration config)
    {
        _apiUrl = config["AppSettings:api_url"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:api_url");
        _email  = config["AppSettings:api_email"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:api_email");
        _apiKey  = config["AppSettings:api_key"]
            ?? throw new InvalidOperationException("Missing config: AppSettings:api_key");
    }

    public async Task RunAsync(string input)
    {
        var (jql, description) = JqlQueryBuilder.BuildQuery(input);
        var csvFileName        = BuildCsvFileName(input);

        File.WriteAllText("response.json", string.Empty);

        InitialiseCsvFile(csvFileName);

        Console.WriteLine($"\n>> {description}");
        Console.WriteLine($">> Output: {csvFileName}\n");

        await FetchAndProcessAllPages(jql, description, csvFileName);

        Console.WriteLine($"\n✅ Done. Report saved to: {csvFileName}");
    }


    private async Task FetchAndProcessAllPages(
        string jql,
        string description,
        string csvFileName)
    {
        int  currentStart = 0;
        bool hasMorePages = true;

        while (hasMorePages)
        {
            var rawJson = await HttpCalls.GetAsync(_apiUrl, _email, _apiKey, jql);

            File.AppendAllText("response.json", Helper.PrettyPrint(rawJson));

            Console.WriteLine("Page fetched successfully.");

            using var doc  = JsonDocument.Parse(rawJson);
            var issues     = doc.RootElement.GetProperty("issues");

            hasMorePages = doc.RootElement.TryGetProperty("isLast", out var isLast)
                           && !isLast.GetBoolean();

            if (hasMorePages)
            {
                jql           = JqlQueryBuilder.AdvancePage(jql, currentStart);
                currentStart += 100;
                Console.WriteLine($"Fetching next page (startAt={currentStart})…");
            }

            PrintSectionHeader(description);

            var csv = ProcessPage(issues);
            File.AppendAllText(csvFileName, csv);
        }
    }

    private static string ProcessPage(JsonElement issues)
    {
        var csv = new StringBuilder();

        foreach (var bug in issues.EnumerateArray())
        {
            var key    = bug.GetProperty("key").GetString()!;
            var fields = bug.GetProperty("fields");

            var environment = GetEnvironment(fields);
            if (string.IsNullOrWhiteSpace(environment)
                || !environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                continue; 
            }

            fields.TryGetProperty("comment", out var commentField);
            var (hasRootCause, hasFix) = JiraCommentHelper.CheckComments(commentField);

            Console.WriteLine($"  Bug {key} → RootCauseInComment:{hasRootCause}  FixInComment:{hasFix}");

            var missing = CollectMissingFields(fields);

            PrintBugSummary(key, missing);

            var missingText = BuildMissingText(missing, hasRootCause, hasFix);

            // if (missingText == "None" && hasRootCause && hasFix)
            // {
                csv.AppendLine(BuildCsvRow(key, missing, fields, hasRootCause, hasFix));
            // }
        }

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

        var rootCause = fields.TryGetProperty("customfield_12608", out var rc)
            ? rc.ToString()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(rootCause) || rootCause == "null")
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
        bool hasFix)
    {
        if (!missing.Any())
        {
            if (!hasRootCause) return "Root Cause(In Comments)";
            if (!hasFix)       return "Fix(In Comments)";
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
        bool hasFix)
    {
        var status      = missing.Any() ? "🔴 Missing" : "✅ All Good";
        var missingText = BuildMissingText(missing, hasRootCause, hasFix);

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
            Helper.Escape(hasFix.ToString())
        );
    }


    private static string BuildCsvFileName(string input)
    {
        var label = JqlQueryBuilder.GetFileLabel(input);
        return $"BugReport_{TimeHelper.Now():yyyy_MM_dd_HH_mm}_{label}.csv";
    }

    private static void InitialiseCsvFile(string fileName)
        => File.WriteAllText(fileName,
            "BugId,Status,MissingFields,RootCause,Fix_Versions,Commits/PR," +
            "GeneratedAtIST,Has_root_cause_in_comments,Has_fix_in_comments\n");


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
