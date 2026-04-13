using System.Numerics;

namespace BugAuditScript.Services;

public static class JqlQueryBuilder
{
    private const string Fields =
        "fixVersions,customfield_11001,customfield_12608,customfield_11900,comment,status";

    private const int PageSize = 100;
    private const int commentPageSize = 20;

    public static (string jql, string description) BuildQuery(string input, string? startDat = "", string? endDate = "") => input switch
    {

        "0" => (Build("updated >= startOfDay()"), "Bugs updated today:"),
        "1" => (Build("updated >= -1d"), "Bugs updated in the last 24 hours:"),
        "3" => (Build("updated >= -3d"), "Bugs updated in the last 3 days:"),
        "4" => (Build("updated >= -4d"), "Bugs updated in the last 4 days:"),
        "5" => (Build("updated >= -5d"), "Bugs updated in the last 5 days:"),
        "6" => (Build("updated >= -6d"), "Bugs updated in the last 6 days:"),
        "7" => (Build("updated >= startOfWeek(-1w)"), "Bugs updated in the last 7 days:"),
        "15" => (Build("updated >= -15d"), "Bugs updated in the last 15 days:"),
        "30" => (Build("updated >= -30d"), "Bugs updated in the last 30 days:"),
        "60" => (Build("updated >= -60d"), "Bugs updated in the last 60 days:"),
        "90" => (Build("updated >= -90d"), "Bugs updated in the last 90 days:"),
        "180" => (Build("updated >= -180d"), "Bugs updated in the last 180 days:"),
        "custom" => (Build(startDat, endDate), "Bugs updated in the last 180 days:"),
        _ => (Build(null), "All Bugs:")
    };

    public static string GetFileLabel(string input, string startDate = "", string endDate = "") => input switch
    {
        "0" => "Today",
        "1" => "Last_24_Hours",
        "3" => "Last_3_Days",
        "4" => "Last_4_Days",
        "5" => "Last_5_Days",
        "6" => "Last_6_Days",
        "7" => "Last_7_Days",
        "15" => "Last_15_Days",
        "30" => "Last_30_Days",
        "60" => "Last_60_Days",
        "90" => "Last_90_Days",
        "180" => "Last_180_Days",
        "custom" => string.IsNullOrEmpty(endDate) ? $"From_{startDate}" : $"From_{startDate}_To_{endDate}",
        _ => "All"
    };

    public static string AdvancePage(string jql, int currentStart)
        => jql.Replace($"startAt={currentStart}", $"startAt={currentStart + PageSize}");


    private static string Build(string? dateFilter)
    {
        string baseJql = dateFilter is null
            ? "issuetype=Bug AND statusCategory = \"Done\" "
            : $"issuetype=Bug AND {dateFilter} AND statusCategory = \"Done\" ";
        baseJql += "AND customfield_11001 = \"Production\"";
        baseJql = Uri.EscapeDataString(baseJql);
        return $"jql={baseJql}&fields={Fields}&maxResults={PageSize}";
    }


    private static string Build(string? startDate, string? endDate)
    {
        string jql = "issuetype=Bug ";
        if (!string.IsNullOrEmpty(startDate))
            jql += $"AND updated >= \"{startDate}\" ";
        if (!string.IsNullOrEmpty(endDate))
            jql += $"AND updated <= \"{endDate}\" ";
        jql += "AND customfield_11001 = \"Production\"";
        jql = Uri.EscapeDataString(jql);
        return $"jql={jql}&fields={Fields}&maxResults={PageSize}";
    }

    public static string getCommentQuery() => $"startAt=0&maxResults={commentPageSize}";
    public static string IntialForAllBugs(string input)
    {
        var newQuery = "issuetype=Bug AND statusCategory = \"Done\" AND cf[11001] = \"Production\"";

        return "jql=" + Uri.EscapeDataString(newQuery) + "&fields=id&maxResults=0";
    }

    public static string getStringCorrect(int startAt) => $"startAt={startAt}&maxResults={commentPageSize}";


    public static string IntialForComments() => "maxResults=0&fields=id";
    public static string advancePageComments(string query, int currentStart) => query.Replace($"startAt={currentStart}", $"startAt={currentStart + commentPageSize}");
}
