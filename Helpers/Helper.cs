using System.Text.Json;

public static class Helper
{
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value) || value=="0" ||value =="{}"||value=="False") return "❌";
        if(value=="True") return "✅";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }
        return value;
    }
     public static string printPretty(string data)
        {
            string prettyJson = JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(data), new JsonSerializerOptions { WriteIndented = true });
            return prettyJson;
        }

     
}