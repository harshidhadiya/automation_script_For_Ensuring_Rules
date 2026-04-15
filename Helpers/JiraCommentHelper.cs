using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Presentation;

namespace BugAuditScript.Helpers;

public static class JiraCommentHelper
{

    public static readonly Regex RootCausePattern = new(
       @"^\s*(root\s*cause|cause|root\s*cause\s*assessment)\s*(?:(?:[:\-])\s*.*|\s*)$",
       RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled
   );
    public  static readonly Regex FixPattern = new(
       @"^\s*(fix\s*applied|applied\s*fix|fix|Fixed|fixes)\s*(?:(?:[:\-])\s*.*|\s*)$",
       RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled
   );

    //   this for the external right now we are not using things
    public static readonly Regex ImpactPattern1 = new(
   @"^\s*leadup\b\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ImpactPattern2 = new(
        @"^\s*preventive\s*action\s*taken\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ImpactPattern3 = new(
        @"^\s*number\s*of\s*business\s*transactions\s*affected\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ImpactPattern4 = new(
        @"^\s*bug\s*timespan\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ImpactPattern5 = new(
        @"^\s*Tentative\s*release\s*date\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ImpactPattern6 = new(
        @"^\s*number\s*of\s*business\s*transactions\s*affected\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    // ─── Public API ───────────────────────────────────────────────────────────
    // this below function is not using we are using the second functions 
    public static (bool hasRootCause, bool hasFix, bool hasImpactDetail) CheckComments(JsonElement commentField, bool isCritical)
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
            text = text.Trim();
            impactedDetails = impactedDetails.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            if (!hasRootCause && RootCausePattern.IsMatch(text))
                hasRootCause = true;

            if (!hasFix && FixPattern.IsMatch(text))
                hasFix = true;


            Console.WriteLine("this is the all the data", ImpactPattern1.IsMatch(impactedDetails), ImpactPattern2.IsMatch(impactedDetails) + impactedDetails);
            if (!hasImpactDetail && ImpactPattern1.IsMatch(impactedDetails) && ImpactPattern2.IsMatch(impactedDetails))
            {
                //    if(isCritical)
                //     {
                //         if(!ImpactPattern3.IsMatch(impactedDetails)&&ImpactPattern4.IsMatch(impactedDetails)&&ImpactPattern5.IsMatch(impactedDetails)&& ImpactPattern6.IsMatch(impactedDetails))
                //         continue;

                //     }

                hasImpactDetail = true;
            }
            if (hasRootCause && hasFix && hasImpactDetail)
                break;
        }

        return (hasRootCause, hasFix, hasImpactDetail);
    }
    public static (bool hasRootCause, bool hasFix, bool hasImpactDetail) CheckComments2(JsonDocument commentField1, bool isCritical)
    {
        bool hasRootCause = false;
        bool hasFix = false;
        bool hasImpactDetail = false;



        if (!commentField1.RootElement.TryGetProperty("comments", out var comments))
            return (false, false, false);


        foreach (var comment in comments.EnumerateArray())
        {
            if (!comment.TryGetProperty("body", out var body))
                continue;

            var (text, impactedDetails) = ExtractPlainText(body);
            // text=text.Trim();
            // impactedDetails=impactedDetails.Trim();
            Console.WriteLine("here is your text");
            Console.WriteLine(text);
            if (string.IsNullOrWhiteSpace(text))
                continue;
            if (!hasRootCause && RootCausePattern.IsMatch(text))
                hasRootCause = true;

            if (!hasFix && FixPattern.IsMatch(text))
                hasFix = true;


            //    Console.WriteLine( "first");
            //    Console.WriteLine(impactedDetails);
            //    Console.WriteLine("second");
            if (!hasImpactDetail && ImpactPattern1.IsMatch(impactedDetails) && ImpactPattern2.IsMatch(impactedDetails))
            {
                //  && ImpactPattern2.IsMatch(impactedDetails)
                //    if(isCritical)
                //     {
                //         if(!ImpactPattern3.IsMatch(impactedDetails)&&ImpactPattern4.IsMatch(impactedDetails)&&ImpactPattern5.IsMatch(impactedDetails)&& ImpactPattern6.IsMatch(impactedDetails))
                //         continue;

                //     }

                hasImpactDetail = true;
            }
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
            string type = block.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "table")
            {
                var impacted = ExtractImpactedDetails(block);
                if (!string.IsNullOrWhiteSpace(impacted))
                {
                    impactedSb.AppendLine(impacted);
                }
            }

            if (type == "text" && block.TryGetProperty("text", out var text))
            {
                sb.Append(text.GetString());
            }

            if (type == "hardBreak")
            {
                sb.AppendLine();
                continue;
            }

            if (type == "inlineCard" && block.TryGetProperty("attrs", out var attrs)
                && attrs.TryGetProperty("url", out var url))
            {
                sb.Append(url.GetString());
            }

            if (type == "mention" && block.TryGetProperty("attrs", out var mentionAttrs)
                && mentionAttrs.TryGetProperty("text", out var mentionText))
            {
                sb.Append(mentionText.GetString());
            }

            if (type == "bulletList")
            {
                foreach (var item in block.GetProperty("content").EnumerateArray())
                {
                    var (itemText, itemImpacted) = ExtractPlainText(item);

                    if (!string.IsNullOrWhiteSpace(itemText))
                    {
                        sb.AppendLine($"- {itemText.Trim()}");
                    }

                    if (!string.IsNullOrWhiteSpace(itemImpacted))
                    {
                        impactedSb.AppendLine(itemImpacted);
                    }
                }
                continue;
            }

            if (block.TryGetProperty("content", out _))
            {
                var (innerText, innerImpacted) = ExtractPlainText(block);

                if (!string.IsNullOrWhiteSpace(innerText))
                    sb.Append(innerText);

                if (!string.IsNullOrWhiteSpace(innerImpacted))
                    impactedSb.AppendLine(innerImpacted);
            }

            if (type == "paragraph")
            {
                sb.AppendLine();
            }
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

                    else if (cellBlock.TryGetProperty("text", out var cellText))
                    {
                        details.Append(cellText.GetString());
                    }
                }

                details.Append("\t");
            }


            details.AppendLine();
        }

        return details.ToString().Trim();
    }
}