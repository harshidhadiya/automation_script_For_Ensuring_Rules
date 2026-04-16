using BugAuditScript.Helpers;
using DocumentFormat.OpenXml.Office.PowerPoint.Y2021.M06.Main;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace BUGAUDITSCRIPT.GoogleSheetUtility
{
    public class GoogleSheet
    {
        private static string spreadsheetId = "1VuLz5vvpX9GIPN1OArSdJqIZ7QmuImGdH-Q_LSbOJOE";
        private static string readRange = "Sheet1!A:J";




        public static SheetsService GetService(string path = "")
        {
            using var stream = File.OpenRead(string.IsNullOrEmpty(path) ? "credentials.json" : path);

            var credential = GoogleCredential.FromStream(stream)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            return new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Bug Sync"
            });
        }

        private static string ExtractBugId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            input = input.Trim();

            if (input.StartsWith("=HYPERLINK", StringComparison.OrdinalIgnoreCase))
            {
                var parts = input.Split('"');
                if (parts.Length >= 4)
                {
                    return parts[3]; 
                }
            }

            return input;
        }

        public static async Task UpsertToGoogleSheet(string csvPath, string credentialPath = "", string id = "", string range = "")
        {
            if (!string.IsNullOrEmpty(id))
                spreadsheetId = id;
            if (!string.IsNullOrEmpty(range))
                readRange = range;
            var service = GetService(credentialPath);

            var response = await service.Spreadsheets.Values
                .Get(spreadsheetId, readRange)
                .ExecuteAsync();

            var values = response.Values ?? new List<IList<object>>();

            var existingMap = new Dictionary<string, int>();

            for (int i = 1; i < values.Count; i++) // skip header
            {
                if (values[i].Count == 0) continue;

                var raw = values[i][0]?.ToString();
                var bugId = ExtractBugId(raw);

                if (!string.IsNullOrWhiteSpace(bugId))
                {
                    existingMap[bugId] = i + 1;
                }
            }

            var csvData = Helper.readCsv(csvPath);

            var updates = new List<ValueRange>();
            var inserts = new List<IList<object>>();

            foreach (var row in csvData)
            {
                if (string.IsNullOrWhiteSpace(row.BugId)) continue;

                var key = ExtractBugId(row.BugId);

                var valuess = new List<object>
                {
                    row.BugId,
                    row.Status,
                    row.MissingFields,
                    row.RootCause,
                    row.FixVersions,
                    row.CommitsPR,
                    row.GeneratedAtIST,
                    row.HasRootCause,
                    row.HasFix,
                    row.HasImpact
                };

                if (existingMap.TryGetValue(key, out int rowIndex))
                {
                    updates.Add(new ValueRange
                    {
                        Range = $"Sheet1!A{rowIndex}:J{rowIndex}",
                        Values = new List<IList<object>> { valuess }
                    });
                }
                else
                {
                    inserts.Add(valuess);
                }
            }

            Console.WriteLine($"Updating: {updates.Count}");
            Console.WriteLine($"Inserting: {inserts.Count}");

            // 🔸 STEP 5: EXECUTE UPDATE
            if (updates.Any())
            {
                var batchUpdate = new BatchUpdateValuesRequest
                {
                    ValueInputOption = "USER_ENTERED",
                    Data = updates
                };

                await service.Spreadsheets.Values
                    .BatchUpdate(batchUpdate, spreadsheetId)
                    .ExecuteAsync();
            }

            // 🔸 STEP 6: EXECUTE INSERT
            if (inserts.Any())
            {
                var appendBody = new ValueRange
                {
                    Values = inserts
                };

                var appendRequest = service.Spreadsheets.Values
                    .Append(appendBody, spreadsheetId, "Sheet1!A:J");

                appendRequest.ValueInputOption =
                    SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

                await appendRequest.ExecuteAsync();
            }

            Console.WriteLine("✅ Google Sheet Sync Completed");
        }
    }
}