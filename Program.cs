using BugAuditScript.Helpers;
using BugAuditScript.Services;
using Microsoft.Extensions.Configuration;


IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("config.json", optional: false, reloadOnChange: true)
    .Build();
Console.WriteLine();
Console.WriteLine("Enter DAYS [ 0 , 1 , 3 , 4 , 5 , 6 , 7 , 30 , 60 , 90 , 180 ] This Will Return Last Days { Your Enter Day } Data");
Console.WriteLine();
Console.WriteLine("Write => * <= for the Enter DateRange Wise Data ");
var input = Console.ReadLine();

if (!Helper.IsValidNumber(input) && input != "custom" && input != "*")
{
    Console.WriteLine("==> Invalid input. Only numbers or 'custom' allowed.");
    return;
}


string startDate = null;
string endDate = null;

if (input == "custom" || input=="*")
{
    input="custom";
    Console.WriteLine("Enter start date ( yyyy-MM-dd ):");
    startDate = Console.ReadLine();

    Console.WriteLine("Enter end date ( yyyy-MM-dd ) optional:");
    endDate = Console.ReadLine();

    if (!string.IsNullOrWhiteSpace(startDate) && !Helper.DateRegex.IsMatch(startDate))
    {
        Console.WriteLine("==> Invalid start date format");
        return;
    }

    if (!string.IsNullOrWhiteSpace(endDate) && !Helper.DateRegex.IsMatch(endDate))
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