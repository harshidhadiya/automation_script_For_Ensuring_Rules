using System.Text.Json;
using System.Text.RegularExpressions;

namespace BugAuditScript.Helpers;


public static class Helper
{
   public  static readonly Regex DateRegex =
     new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
    public static bool IsValidNumber(string input)
    {
        bool flags=int.TryParse(input, out var datas);
        var data=new List<int>{1,0,3,4,5,6,7,15,30,60,90,180};
        if(flags && !data.Contains(datas)) return false;
        return flags;
    }
   public  static bool IsStartLessThanEnd(string start, string end)
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


    public static string PrettyPrint(string json)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(
            JsonSerializer.Deserialize<object>(json),
            options);
    }
}