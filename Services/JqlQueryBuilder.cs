using System.Numerics;

namespace BugAuditScript.Services;

public static class JqlQueryBuilder
{
    private const string Fields =
        "fixVersions,customfield_11001,customfield_12608,customfield_11900,comment";

    private const int PageSize = 100;


    public static (string jql, string description) BuildQuery(string input) => input switch
    {
      
        "0"  => (Build("updated >= startOfDay()"),     "Bugs updated today:"),
        "1"  => (Build("updated >= -1d"),               "Bugs updated in the last 24 hours:"),
        "7"  => (Build("updated >= -7d"),               "Bugs updated in the last 7 days:"),
        "15" => (Build("updated >= -15d"),              "Bugs updated in the last 15 days:"),
        _    => (Build(null),                           "All Bugs:")
    };

    public static string GetFileLabel(string input) => input switch
    {
        "0"  => "Today",
        "1"  => "Last_24_Hours",
        "7"  => "Last_7_Days",
        "15" => "Last_15_Days",
        _    => "All"
    };

    public static string AdvancePage(string jql, int currentStart)
        => jql.Replace($"startAt={currentStart}", $"startAt={currentStart + PageSize}");


    private static string Build(string? dateFilter)
    {
        string baseJql = dateFilter is null
            ? "issuetype=Bug"
            : $"issuetype=Bug AND {dateFilter}";

        return $"jql={baseJql}&fields={Fields}&maxResults={PageSize}&startAt=0";
    }
}
