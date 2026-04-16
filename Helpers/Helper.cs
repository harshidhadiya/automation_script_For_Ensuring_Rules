using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BUGAUDITSCRIPT;
using DocumentFormat.OpenXml.Office.PowerPoint.Y2021.M06.Main;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.VisualBasic.FileIO;

namespace BugAuditScript.Helpers;


public static class Helper
{
    public static readonly Regex DateRegex =
      new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
    public static bool IsValidDate(string input)
    {
        if (!DateRegex.IsMatch(input))
            return false;

        if (!DateTime.TryParseExact(
                input,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
            return false;

        // Step 3: Prevent future dates
        if (parsedDate.Date > DateTime.Today)
            return false;

        return true;
    }
    public static bool IsValidNumber(string input)
    {
        bool flags = int.TryParse(input, out var datas);
        var data = new List<int> { 1, 0, 3, 4, 5, 6, 7, 15, 30, 60, 90, 180 };
        if (flags && !data.Contains(datas)) return false;
        return flags;
    }
    public static bool IsStartLessThanEnd(string start, string end)
    {
        var s = DateTime.Parse(start);
        var e = DateTime.Parse(end);
        return s < e;
    }
    public static string Escape(string value, bool flag = false)
    {
        if (string.IsNullOrEmpty(value)
            || value == "0"
            || value == "{}"
            || value == "False")
        {
            return "❌";
        }

        if (value == "True" || !flag)
            return "✅";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }
    public static string MakeJiraLink(string value)
    {
        var baseUrl = "https://peddle.atlassian.net";

        return $"\"=HYPERLINK(\"\"{baseUrl}/browse/{value}\"\",\"\"{value}\"\")\"";
    }


    public static string PrettyPrint(string json)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(
            JsonSerializer.Deserialize<object>(json),
            options);
    }


    // this would help for the finding out the all the fields in the row

    public static List<BugRow> readCsv(string path)
    {
        List<BugRow> list=new ();
        using var parser=new TextFieldParser(path);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes=true;
        parser.ReadLine();
        while(!parser.EndOfData)
        {
            var col=parser.ReadFields();
            if(col!=null && col.Length!=0)
            list.Add(new BugRow
            {
                BugId=col[0],
                Status=col[1],
                MissingFields=col[2],
                RootCause=col[3],
                FixVersions=col[4],
                CommitsPR=col[5],
                GeneratedAtIST=col[6],
                HasRootCause=col[7],
                HasFix=col[8],
                HasImpact=col[9]
            });
        }
        return list;
    }
}