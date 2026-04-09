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


    private static readonly Regex ImpactPattern = new(
      @"^\s*(number\s*of\s*business\s*transactions\s*affected|leadup\s*|bug\s*timespan|preventive\s*action\s*taken)\s*",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    // ─── Public API ───────────────────────────────────────────────────────────

    public static (bool hasRootCause, bool hasFix, bool hasImpactDetail) CheckComments(JsonElement commentField)
    {
        bool hasRootCause = false;
        bool hasFix = false;
        bool hasImpactDetail = false;

        if (!commentField.TryGetProperty("comments", out var comments))
            return (false, false, false);

        foreach (var comment in comments.EnumerateArray())
        {
            if (!comment.TryGetProperty("body", out var body))
                continue;

            var (text, impactedDetails) = ExtractPlainText(body);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (!hasRootCause && RootCausePattern.IsMatch(text))
                hasRootCause = true;

            if (!hasFix && FixPattern.IsMatch(text))
                hasFix = true;
            File.AppendAllText("impactedDetails.txt", impactedDetails);
            if (!hasImpactDetail && ImpactPattern.IsMatch(impactedDetails))
                hasImpactDetail = true;

            if (hasRootCause && hasFix && hasImpactDetail)
                break;
        }

        return (hasRootCause, hasFix, hasImpactDetail);
    }

    private static (string plainText, string impactedDetails) ExtractPlainText(JsonElement node)
    {
        var sb = new StringBuilder();
        var impactedSb = new StringBuilder();

        if (!node.TryGetProperty("content", out var content))
            return (string.Empty, string.Empty);

        foreach (var block in content.EnumerateArray())
        {
            // 🔥 Detect TABLE properly
            if (block.TryGetProperty("type", out var type) && type.GetString() == "table")
            {
                Console.WriteLine("  → Found a table in comments, extracting impacted details...");
                impactedSb.AppendLine(ExtractImpactedDetails(block));
                Console.WriteLine(impactedSb.ToString());
                continue;
            }

            // 🔥 Direct text node
            if (block.TryGetProperty("text", out var text))
            {
                sb.Append(text.GetString());
            }

            // 🔥 Recursive handling
            if (block.TryGetProperty("content", out _))
            {
                var (innerText, innerImpacted) = ExtractPlainText(block);

                sb.Append(innerText);
                impactedSb.Append(innerImpacted);
            }

            sb.AppendLine();
        }

        return (sb.ToString().Trim(), impactedSb.ToString().Trim());
    }
    private static string ExtractImpactedDetails(JsonElement node)
    {
        if (!node.TryGetProperty("content", out var rows))
            return string.Empty;

        var details = new StringBuilder();

        foreach (var row in rows.EnumerateArray())
        {
            if (!row.TryGetProperty("content", out var cells))
                continue;

            foreach (var cell in cells.EnumerateArray())
            {
                if (!cell.TryGetProperty("content", out var cellContent))
                    continue;

                foreach (var cellBlock in cellContent.EnumerateArray())
                {
                    // 🔥 Handle paragraph correctly
                    if (cellBlock.TryGetProperty("type", out var type) && type.GetString() == "paragraph")
                    {
                        if (cellBlock.TryGetProperty("content", out var paraContent))
                        {
                            foreach (var para in paraContent.EnumerateArray())
                            {
                                if (para.TryGetProperty("text", out var paraText))
                                {
                                    details.Append(paraText.GetString());
                                }
                            }
                        }
                    }
                    // 🔥 Direct text fallback
                    else if (cellBlock.TryGetProperty("text", out var cellText))
                    {
                        details.Append(cellText.GetString());
                    }
                }

                details.Append("\t"); // column separator
            }

            details.AppendLine(); // row separator
        }

        return details.ToString().Trim();
    }
}