namespace BugAuditScript.Helpers;

/// <summary>
/// Centralises time-zone handling for the application.
/// All timestamps written to CSVs and logs use Indian Standard Time (IST).
///
/// To change the time zone, update <see cref="IndiaZone"/> with the correct
/// IANA identifier (e.g. "America/Chicago").
/// </summary>
public static class TimeHelper
{
    // ─── Configuration ────────────────────────────────────────────────────────

    /// <summary>
    /// The application's canonical time zone.
    /// Change this if the team is in a different region.
    /// </summary>
    private static readonly TimeZoneInfo IndiaZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current date-and-time converted to IST.
    /// Use this everywhere a "now" value is needed to ensure consistency.
    /// </summary>
    public static DateTime Now()
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaZone);
}