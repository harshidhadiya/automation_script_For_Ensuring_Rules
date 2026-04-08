using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using BUGAUDITSCRIPT.Helpers;
using Microsoft.Extensions.Configuration;

var input = args.Length > 0 ? args[0] : "Nothing";
input = input.Trim();
var gap = 100;
var start = 0;
var fieldsDictonary = new Dictionary<string, string>
 {
    {"summary","Summary"},
    {"status","Status"},
    {"description","Description"},
    {"comment","Comment"},
    {"fixVersions","Fix Versions"},
    {"customfield_11001","Environment"},
    {"customfield_12608","Root cause"},
    {"customfield_11900","Development"}
 };
// 🎯 Switch for JQL
var (jql, message) = input switch
{
    "0" => ("jql=issuetype=Bug AND updated>= startOfDay()&fields=fixVersions,customfield_11001,customfield_12608,customfield_11900,comment&maxResults=100&startAt=0", "Bugs updated today:"),
    "1" => ("jql=issuetype=Bug AND updated >= -1d&fields=fixVersions,customfield_11001,customfield_12608,customfield_11900,comment&maxResults=100&startAt=0", "Bugs updated in the last 24 hours:"),
    "7" => ("jql=issuetype=Bug AND updated >= -7d&fields=fixVersions,customfield_11001,customfield_12608,customfield_11900,comment&maxResults=100&startAt=0", "Bugs updated in the last 7 days:"),
    "15" => ("jql=issuetype=Bug AND updated >= -15d&fields=fixVersions,customfield_11001,customfield_12608,customfield_11900,comment&maxResults=100&startAt=0", "Bugs updated in the last 15 days:"),
    _ => ("jql=issuetype=Bug&fields=fixVersions,customfield_11001,customfield_12608,customfield_11900,comment&maxResults=100&startAt=0", "All Bugs:")
};
var fileAdder = input switch
{
    "0" => "Today",
    "1" => "Last_24_Hours",
    "7" => "Last_7_Days",
    "15" => "Last_15_Days",
    _ => "All"
};
Console.WriteLine(jql);
Console.WriteLine("");

IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("config.json", optional: false, reloadOnChange: true)
    .Build();


string apiUrl = config["AppSettings:api_url"]!;
string email = config["AppSettings:api_email"]!;
string apiKey = config["AppSettings:api_key"]!;
string testurl = config["AppSettings:test_url"]!;
string testurl2 = config["AppSettings:test_url2"]!;
Console.WriteLine(email);

if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("API URL, Email, or API Key is missing in the configuration.");
    return;
}
var hasData = true;





    var responseString = await HttpCalls.GetAsync(apiUrl, email, apiKey, jql);
    Console.WriteLine("Data fetched from API successfully.");
    var prettyResponse = Helper.printPretty(responseString);
    File.WriteAllText("response.json", prettyResponse);





    using var doc = JsonDocument.Parse(responseString);
    var bugs = doc.RootElement.GetProperty("issues");
    hasData = doc.RootElement.TryGetProperty("isLast", out var isLast) && !isLast.GetBoolean();
    if (hasData)
    {
        jql = jql.Replace($"startAt={start}", $"startAt={start + gap}");
        start += gap;
        Console.WriteLine(start);
    }
    Console.WriteLine();
    Console.WriteLine(message);
    Console.WriteLine();
    Console.WriteLine("------------------------------");
    Console.WriteLine();
    var csv = new StringBuilder();
    csv.AppendLine("BugId,Status,MissingFields,RootCause,Fix,Commits/PR,GeneratedAtIST,Has_root_cause_in_comments,Has_fix_in_comments");



    var datas = bugs.EnumerateArray().Where(x => x.GetProperty("fields").TryGetProperty("customfield_11001", out var env) && !string.IsNullOrWhiteSpace(env.ToString()) && !env.ToString().Equals("Production", StringComparison.OrdinalIgnoreCase));
    foreach (var b in datas)
    {
        var key = b.GetProperty("key").GetString();
        var fields = b.GetProperty("fields");
        var environment = fields.TryGetProperty("customfield_11001", out var env) ? env.ToString() : "";
        if (string.IsNullOrWhiteSpace(environment) || environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }
        fields.TryGetProperty("comment",out var comments);


        var (hasRootCause, hasFix) = JiraCommentHelper.CheckComments(comments);
        Console.WriteLine($"Bug {key} → Has Root Cause: {hasRootCause}, Has Fix: {hasFix}");
        var rootCause = fields.TryGetProperty("customfield_12608", out var rc) ? rc.ToString() : "";
        var fix = fields.TryGetProperty("fixVersions", out var fx) ? fx.EnumerateArray().Count() : 0;
        var pr = fields.TryGetProperty("customfield_11900", out var prEl) ? prEl.ToString() : "";



        Console.WriteLine("-------------------------------");
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(rootCause))
            missing.Add("Root Cause");

        if (fix == 0)
            missing.Add("Fix Version");


        if (string.IsNullOrWhiteSpace(pr) || pr.Equals("{}", StringComparison.OrdinalIgnoreCase))
            missing.Add("Commits/PR");

        Console.Write("=> ");

        if (missing.Any())
        {
            Console.WriteLine($"Bug {key}:");

            foreach (var m in missing)
            {
                Console.WriteLine($" - Missing {m}");
            }
        }
        else
        {
            Console.WriteLine($"Bug {key} → ✅ All Good");
        }

        Console.WriteLine();
        Console.WriteLine("------------------------------");
        Console.WriteLine();
        var status = missing.Any() ? "🔴 Missing" : "✅ All Good";
        var missingText = missing.Any() ? string.Join(" | ", missing) : "None";
        missingText=missingText.Replace("Root Cause ","Root Cause(In Fields) ");
        if(missingText.Equals("None") && !hasRootCause) missingText="Root Cause(In Comments)";
         if(missingText.Equals("None") && !hasFix) missingText="Fix(In Comments)";
        //  if(!missingText.Contains("None") || !hasRootCause || !hasFix) continue ;
        csv.AppendLine(string.Join(",",
            Helper.Escape(key),
            Helper.Escape(status),
            Helper.Escape(missingText),
            Helper.Escape(rootCause),
            Helper.Escape(fix.ToString()),
            Helper.Escape(pr),
            Helper.Escape(TimeHelper.Now().ToString("yyyy-MM-dd HH:mm:ss")),
            Helper.Escape(hasRootCause.ToString()),
            Helper.Escape(hasFix.ToString())
        ));
    }
    var fileName = $"BugReport_{TimeHelper.Now().ToString("yyyy_MM_dd")}_{fileAdder}.csv";
    File.WriteAllText(fileName, csv.ToString());

    Console.WriteLine();
    Console.WriteLine($"✅ CSV Generated: {fileName}");


