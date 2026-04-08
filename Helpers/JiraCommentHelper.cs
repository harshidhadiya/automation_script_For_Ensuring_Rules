using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BugAuditScript.Helpers;

public static class JiraCommentHelper
{
    
    private static readonly Regex RootCausePattern = new(
        @"^\s*(root\s*cause|cause|root\s*cause\s*assessment)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex FixPattern = new(
        @"^\s*(fix\s*applied|applied\s*fix|fix)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    // ─── Public API ───────────────────────────────────────────────────────────

    public static (bool hasRootCause, bool hasFix) CheckComments(JsonElement commentField)
    {
        bool hasRootCause = false;
        bool hasFix       = false;

        if (!commentField.TryGetProperty("comments", out var comments))
            return (false, false);

        foreach (var comment in comments.EnumerateArray())
        {
            if (!comment.TryGetProperty("body", out var body))
                continue;

            var text = ExtractPlainText(body);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (!hasRootCause && RootCausePattern.IsMatch(text))
                hasRootCause = true;

            if (!hasFix && FixPattern.IsMatch(text))
                hasFix = true;

            if (hasRootCause && hasFix)
                break;
        }

        return (hasRootCause, hasFix);
    }

    private static string ExtractPlainText(JsonElement node)
    {
        var sb = new StringBuilder();

        if (!node.TryGetProperty("content", out var content))
            return sb.ToString();

        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("text", out var text))
            {
                sb.Append(text.GetString());
            }
            else
            {
                // Recurse into nested content (e.g. listItem, tableCell …)
                sb.Append(ExtractPlainText(block));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}