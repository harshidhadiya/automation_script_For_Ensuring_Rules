using System.Numerics;

namespace BugAuditScript.Services;

public static class JqlQueryBuilder
{
    private const string Fields =
        "fixVersions,customfield_11001,customfield_12608,customfield_11900,comment,status";

    private const int PageSize = 100;


    public static (string jql, string description) BuildQuery(string input) => input switch
    {
      
        "0"  => (Build("updated >= startOfDay()"),     "Bugs updated today:"),
        "1"  => (Build("updated >= -1d"),               "Bugs updated in the last 24 hours:"),
        "7"  => (Build("updated >= -7d"),               "Bugs updated in the last 7 days:"),
        "15" => (Build("updated >= -15d"),              "Bugs updated in the last 15 days:"),
        "30" => (Build("updated >= -30d"),              "Bugs updated in the last 30 days:"),
        "60" => (Build("updated >= -60d"),              "Bugs updated in the last 60 days:"),
        "90" => (Build("updated >= -90d"),              "Bugs updated in the last 90 days:"),
        "180" => (Build("updated >= -180d"),            "Bugs updated in the last 180 days:"),
        _    => (Build(null),                           "All Bugs:")
    };

    public static string GetFileLabel(string input) => input switch
    {
        "0"  => "Today",
        "1"  => "Last_24_Hours",
        "7"  => "Last_7_Days",
        "15" => "Last_15_Days",
        "30" => "Last_30_Days",
        "60" => "Last_60_Days",
        "90" => "Last_90_Days",
        "180" => "Last_180_Days",
        _    => "All"
    };

    public static string AdvancePage(string jql, int currentStart)
        => jql.Replace($"startAt={currentStart}", $"startAt={currentStart + PageSize}");


    private static string Build(string? dateFilter)
    {
        string baseJql = dateFilter is null
            ? "issuetype=Bug AND statusCategory = \"Done\""
            : $"issuetype=Bug AND {dateFilter} AND statusCategory = \"Done\"";
        baseJql=Uri.EscapeDataString(baseJql);
        return $"jql={baseJql}&fields={Fields}&maxResults={PageSize}&startAt=0";
    }
}
