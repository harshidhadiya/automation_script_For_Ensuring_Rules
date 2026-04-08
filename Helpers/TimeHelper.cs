using System.Text.Json;

namespace BUGAUDITSCRIPT.Helpers
{
    
 public static class TimeHelper
{
    private static readonly TimeZoneInfo IndiaZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateTime Now()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaZone);
    }
   
}
}