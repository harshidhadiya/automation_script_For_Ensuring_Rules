using System.Text.Json;

namespace BugAuditScript.Helpers;


public static class Helper
{
   
    public static string Escape(string value,bool flag=false)
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

        return value ;
    }

   
    public static string PrettyPrint(string json)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(
            JsonSerializer.Deserialize<object>(json),
            options);
    }
}