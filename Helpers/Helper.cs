using System.Text.Json;

namespace BugAuditScript.Helpers;

/// <summary>
/// General-purpose utility methods for formatting and CSV escaping.
/// </summary>
public static class Helper
{
    // ─── CSV helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escapes a value for safe inclusion in a CSV cell.
    /// <list type="bullet">
    ///   <item>Empty / "0" / "{}" / "False" → ❌</item>
    ///   <item>"True"                        → ✅</item>
    ///   <item>Values containing commas, quotes, or newlines are double-quoted
    ///         with internal quotes escaped.</item>
    ///   <item>All other values returned as-is.</item>
    /// </list>
    /// </summary>
    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)
            || value == "0"
            || value == "{}"
            || value == "False")
        {
            return "❌";
        }

        if (value == "True")
            return "✅";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }

    // ─── JSON helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Pretty-prints a JSON string with indentation.
    /// Used to write human-readable output to <c>response.json</c>.
    /// </summary>
    public static string PrettyPrint(string json)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(
            JsonSerializer.Deserialize<object>(json),
            options);
    }
}