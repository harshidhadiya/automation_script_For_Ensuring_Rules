namespace BugAuditScript.Models;

/// <summary>
/// Represents a Jira JQL search request body used for POST requests.
/// </summary>
public class JiraSearchRequest
{
    /// <summary>JQL query string.</summary>
    public string Jql { get; set; } = string.Empty;

    /// <summary>Maximum number of results to return per page.</summary>
    public int MaxResults { get; set; } = 100;

    /// <summary>Zero-based index of the first result to return.</summary>
    public int StartAt { get; set; } = 0;

    /// <summary>List of field keys to include in the response.</summary>
    public List<string> Fields { get; set; } = new();

    /// <summary>Whether the fields list uses field keys instead of field IDs.</summary>
    public bool FieldsByKeys { get; set; } = false;

    /// <summary>Optional list of expand values (e.g. renderedFields, changelog).</summary>
    public List<string> Expand { get; set; } = new();

    /// <summary>Token for the next page (used for cursor-based pagination).</summary>
    public string? NextPageToken { get; set; }
}
