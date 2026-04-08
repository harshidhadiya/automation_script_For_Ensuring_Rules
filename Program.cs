using BugAuditScript.Services;
using Microsoft.Extensions.Configuration;


IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("config.json", optional: false, reloadOnChange: true)
    .Build();
var input = (args.Length > 0 ? args[0] : string.Empty).Trim();

var runner = new BugAuditRunner(config);
await runner.RunAsync(input);