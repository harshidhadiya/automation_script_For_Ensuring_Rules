using BugAuditScript.Helpers;
using BugAuditScript.Services;
using Microsoft.Extensions.Configuration;
//   var ans=JiraCommentHelper.FixPattern.IsMatch(" fixes - ");
//   var ans1=JiraCommentHelper.FixPattern.IsMatch("fixes - adf ");
//   var ans2=JiraCommentHelper.FixPattern.IsMatch("fixes sdf ");
//   var ans3=JiraCommentHelper.FixPattern.IsMatch(" fixes ");
//   var ans4=JiraCommentHelper.FixPattern.IsMatch("fixes - ");

//   Console.WriteLine(ans);
//   Console.WriteLine(ans1);
//   Console.WriteLine(ans2);
//   Console.WriteLine(ans3);
//   Console.WriteLine(ans4);
  
//   return ;





IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("config.json", optional: false, reloadOnChange: true)
    .Build();
Console.WriteLine();
Console.WriteLine("==============================================");
Console.WriteLine("              DATA FETCH OPTIONS             ");
Console.WriteLine("==============================================");

Console.WriteLine();
Console.WriteLine(" ## Option 1: Fetch by Days ##");
Console.WriteLine("----------------------------------------------");
Console.WriteLine("Enter number of days:");
Console.WriteLine("[0, 1, 2, 3, 4, 5, 6, 7, 30, 60, 90, 180]");
Console.WriteLine();
Console.WriteLine("# Example:");
Console.WriteLine("   Enter 7 → You will get data from last 7 days");

Console.WriteLine();
Console.WriteLine(" ## Option 2: Fetch by Date Range ##");
Console.WriteLine("----------------------------------------------");
Console.WriteLine("Enter: *");
Console.WriteLine();
Console.WriteLine("Then provide:");
Console.WriteLine("   From Date (required)");
Console.WriteLine("   To Date   (optional → default = today)");
Console.WriteLine();
Console.WriteLine("# Example:");
Console.WriteLine("   Enter * → Then input dates when prompted");

Console.WriteLine();
Console.WriteLine("==============================================");
var input = Console.ReadLine();

if (!Helper.IsValidNumber(input) && input != "custom" && input != "*")
{
    Console.WriteLine("==> Invalid input. Only numbers or '*' allowed.");
    return;
}


string startDate = null;
string endDate = null;

if (input == "custom" || input=="*")
{
    input="custom";
    Console.WriteLine("Enter start date ( yyyy-MM-dd ):");
    startDate = Console.ReadLine();

    if (!string.IsNullOrWhiteSpace(startDate) && !Helper.IsValidDate(startDate))
    {
        Console.WriteLine("==> Invalid start date format");
        return;
    }
    Console.WriteLine("Enter end date ( yyyy-MM-dd ) optional:");
    endDate = Console.ReadLine();


    if (!string.IsNullOrWhiteSpace(endDate) && !Helper.IsValidDate(endDate))
    {
        Console.WriteLine("==> Invalid end date format");
        return;
    }

    if (!string.IsNullOrWhiteSpace(endDate) && string.IsNullOrWhiteSpace(startDate))
    {
        Console.WriteLine("==>  End date cannot exist without start date");
        return;
    }

    if (!string.IsNullOrWhiteSpace(startDate) &&
        !string.IsNullOrWhiteSpace(endDate))
    {
        if (!Helper.IsStartLessThanEnd(startDate, endDate))
        {
            Console.WriteLine("==> Start date must be LESS than end date");
            return;
        }
    }
}
Console.WriteLine(input);
var runner = new BugAuditRunner(config);
await runner.RunAsync(input,startDate,endDate);