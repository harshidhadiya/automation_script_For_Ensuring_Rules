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
        private static string readRange = "Sheet1!A:J";




        public static SheetsService GetService(string path)
        {

         
            if(string.IsNullOrEmpty(path))
            throw new InvalidDataException("Credentials Path Cannot be empty");


            if(!PathUtility.FileExistsStrict(path))
            throw new InvalidDataException($"{path}File Is Not Exist So ensure that file contains path");

            using var stream = File.OpenRead(path);

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

        public static async Task UpsertToGoogleSheet(
                                                     string csvPath,
                                                     string credentialPath,
                                                     string spreadsheetId)
        {
            

            readRange=$"Sheet1!A:{GetColumnLetter(Helper.fields.Count())}";
                Console.WriteLine(credentialPath + "this is the credential path ");
                 Console.WriteLine("id of the spreadsheet"+spreadsheetId);
            Helper.Log("SpreadSheet Id => "+spreadsheetId);
               
            var service = GetService(credentialPath);

         
            var expectedHeaders = Helper.fields;

            // 🔹 STEP 1: GET EXISTING DATA
            var response = await service.Spreadsheets.Values
                .Get(spreadsheetId, readRange)
                .ExecuteAsync();

            var values = response.Values ?? new List<IList<object>>();

            // 🔹 STEP 2: HANDLE HEADER
            bool headerMissing = values.Count == 0 || values[0].Count == 0;

            if (headerMissing)
            {
                Helper.Log("Header missing → creating header");

                var headerBody = new ValueRange
                {
                    Values = new List<IList<object>> { expectedHeaders.Cast<object>().ToList() }
                };

                var headerRequest = service.Spreadsheets.Values
                    .Update(headerBody, spreadsheetId, "Sheet1!A1");

                headerRequest.ValueInputOption =
                    SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

                await headerRequest.ExecuteAsync();

                values.Insert(0, expectedHeaders.Cast<object>().ToList());
            }
            else
            {
                var existingHeaders = values[0].Select(h => h.ToString()).ToList();

                if (!existingHeaders.SequenceEqual(expectedHeaders))
                {
                    Helper.Log("Header mismatch → updating header");

                    var headerBody = new ValueRange
                    {
                        Values = new List<IList<object>> { expectedHeaders.Cast<object>().ToList() }
                    };

                    var headerRequest = service.Spreadsheets.Values
                        .Update(headerBody, spreadsheetId, "Sheet1!A1");

                    headerRequest.ValueInputOption =
                        SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

                    await headerRequest.ExecuteAsync();

                    values[0] = expectedHeaders.Cast<object>().ToList();
                }
            }

            // 🔹 STEP 3: HEADER INDEX MAP (DYNAMIC)
            var headerIndex = values[0].Select((h, i) => new { Name = h.ToString(), Index = i }).ToDictionary(x => x.Name, x => x.Index);

            // 🔹 STEP 4: BUILD EXISTING MAP
            var existingMap = new Dictionary<string, int>();

            for (int i = 1; i < values.Count; i++)
            {
                if (values[i].Count == 0) continue;

                int bugIdIndex = headerIndex["BugId"];

                if (values[i].Count <= bugIdIndex) continue;

                var raw = values[i][bugIdIndex]?.ToString();
                var bugId = ExtractBugId(raw);

                if (!string.IsNullOrWhiteSpace(bugId))
                {
                    existingMap[bugId] = i + 1;
                }
            }

            // 🔹 STEP 5: READ CSV
            var csvData = Helper.readCsv(csvPath);

            var updates = new List<ValueRange>();
            var inserts = new List<IList<object>>();

            // 🔹 HELPER: BUILD ROW DYNAMICALLY
            List<object> BuildRow(dynamic row)
            {
                return expectedHeaders.Select(header => header switch
                {
                    "BugId" => row.BugId,
                    "Status" => row.Status,
                    "MissingFields" => row.MissingFields,
                    "RootCause" => row.RootCause,
                    "Fix_Versions" => row.FixVersions,
                    "Commits/PR" => row.CommitsPR,
                    "GeneratedAtIST" => row.GeneratedAtIST,
                    "Has_root_cause_in_comments" => row.HasRootCause,
                    "Has_fix_in_comments" => row.HasFix,
                    "Has_impact_details_in_comments" => row.HasImpact,
                    _ => ""
                }).Cast<object>().ToList();
            }

            // 🔹 STEP 6: PROCESS DATA
            foreach (var row in csvData)
            {
                if (string.IsNullOrWhiteSpace(row.BugId)) continue;

                var key = ExtractBugId(row.BugId);
                var valuess = BuildRow(row);

                if (existingMap.TryGetValue(key, out int rowIndex))
                {
                    string endColumn = GetColumnLetter(expectedHeaders.Count);

                    updates.Add(new ValueRange
                    {
                        Range = $"Sheet1!A{rowIndex}:{endColumn}{rowIndex}",
                        Values = new List<IList<object>> { valuess }
                    });
                }
                else
                {
                    inserts.Add(valuess);
                }
            }

            Helper.Log($"Updating: {updates.Count}");
            Helper.Log($"Inserting: {inserts.Count}");

            Console.WriteLine($"Updating: {updates.Count}");
            Console.WriteLine($"Inserting: {inserts.Count}");

            // 🔸 STEP 7: UPDATE
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

            // 🔸 STEP 8: INSERT
            if (inserts.Any())
            {
                string endColumn = GetColumnLetter(expectedHeaders.Count);

                var appendBody = new ValueRange
                {
                    Values = inserts
                };

                var appendRequest = service.Spreadsheets.Values
                    .Append(appendBody, spreadsheetId, $"Sheet1!A:{endColumn}");

                appendRequest.ValueInputOption =
                    SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

                await appendRequest.ExecuteAsync();
            }

            Helper.Log("✅ Google Sheet Sync Completed");
            Console.WriteLine("✅ Google Sheet Sync Completed");
        }

        private static string GetColumnLetter(int col)
        {
            string column = "";
            while (col > 0)
            {
                col--;
                column = (char)('A' + (col % 26)) + column;
                col /= 26;
            }
            return column;
        }
    }
}