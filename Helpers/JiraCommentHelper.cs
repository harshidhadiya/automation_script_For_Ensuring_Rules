using System;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class JiraCommentHelper
{
    public static (bool hasRootCause, bool hasFix) CheckComments(JsonElement commentField)
    {
        bool hasRootCause = false;
        bool hasFix = false;

        if (!commentField.TryGetProperty("comments", out JsonElement comments))
            return (false, false);

        foreach (var comment in comments.EnumerateArray())
        {
            if (!comment.TryGetProperty("body", out JsonElement body))
                continue;

            string text = ExtractPlainText(body);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            // ROOT CAUSE patterns
            if (Regex.IsMatch(text, @"^\s*(root\s*cause|cause)\s*:", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                hasRootCause = true;
            }

            if (Regex.IsMatch(text, @"^\s*(fix\s*applied|applied\s*fix|fix)\s*:\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                hasFix = true;
            }

            // Early exit if both found
            if (hasRootCause && hasFix)
                break;
        }

        return (hasRootCause, hasFix);
    }

    private static string ExtractPlainText(JsonElement body)
    {
        StringBuilder sb = new StringBuilder();
        if (body.TryGetProperty("content", out JsonElement content))
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type))
                {
                    if (block.TryGetProperty("content", out var innerContent))
                    {
                        foreach (var node in innerContent.EnumerateArray())
                        {
                            if (node.TryGetProperty("type", out var nodeType))
                            {
                                if (node.TryGetProperty("text", out var text))
                                {
                                    sb.Append(text.GetString());
                                }
                                else
                                {
                                    sb.Append(ExtractPlainText(node));
                                }
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}