using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Cake.Core; // Make sure Cake.Core is referenced in your project
using Cake.Core.IO;
using System.Diagnostics; // Include for DirectoryPath

class Program
{
    public static List<string> widgets
    {
        get
        {
            return new List<string>()
            {
                //"SfChat",
                "SfAIAssistView",
                //"SfBarcodeGenerator",
                "SfCalendar",
                //"SfDateRangePicker",
                //"SfCartesianCharts",
                //"SfTreemap"
            };
        }
    }
    public static Dictionary<string, (int total, int passed, int failed)> widgetver =
    new Dictionary<string, (int, int, int)>();
    public static List<string> versions = new List<string>();
    static void Main(string[] args)
    {
        //FlutterTestRunner.Run();
        string filePath = "../../../index.html";
        string baseDirectory = "../../../ControlIndex";
        string pathtxt = "../../../Reports";
        string inputVersion = "";

        if (File.Exists(filePath))
        {
            versions = ExtractExistingVersions(filePath);
        }

        Console.WriteLine("Enter new versions to add (type 'done' to finish):");
        while (true)
        {
            string input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input) || input.ToLower() == "done") break;

            if (!versions.Contains(input)) // Avoid duplicate entries
            {
                versions.Add(input);
                inputVersion = input;
                CreateVersionFolder(baseDirectory, input, widgets);
            }
        }

        GenerateIndexHtml(versions, filePath);
        Console.WriteLine("index.html updated successfully!");

        if (!string.IsNullOrEmpty(inputVersion))
        {
            GenerateTXTFileForWidgets(pathtxt, baseDirectory, inputVersion, widgets);
        }

        foreach (string widget in widgets)
        {

            // Integrate HTML report generation for SfBarcodeGenerator
            string testFilePath = System.IO.Path.Combine("../../../Reports", inputVersion, widget, $"{widget}.txt");
            string reportHtmlPath = $"../../../ControlIndex/{inputVersion}/{widget}/{widget}.html";

            var testGroups = ParseTestFiles(testFilePath);
            var groupSummary = testGroups.ToDictionary(kv => kv.Key, kv => (kv.Value.total, kv.Value.passed, kv.Value.failed));
            string summaryReportPath = $"{baseDirectory}/{inputVersion}/{widget}/{widget}.html";
            Directory.CreateDirectory($"{baseDirectory}/{inputVersion}/{widget}");
            GenerateSummaryHtml(groupSummary, summaryReportPath, inputVersion);
            GenerateGroupHtmlReports(testGroups, baseDirectory, inputVersion, widget);
            //Directory.CreateDirectory(System.IO.Path.GetDirectoryName(reportHtmlPath) ?? baseDirectory);
            //File.WriteAllText(reportHtmlPath, htmlContent);
        }

        Console.WriteLine("Test report generated successfully!");
    }

    static List<string> ExtractExistingVersions(string filePath)
    {
        List<string> existingVersions = new List<string>();
        string[] lines = File.ReadAllLines(filePath);

        foreach (string line in lines)
        {
            if (line.Contains("ControlIndex/"))
            {
                int start = line.IndexOf("ControlIndex/") + 13;
                int end = line.IndexOf("/index.html", start);
                if (start > 12 && end > start)
                {
                    string version = line.Substring(start, end - start);
                    Console.WriteLine(version);
                    existingVersions.Add(version);
                }
            }
        }
        return existingVersions;
    }

    static void CreateVersionFolder(string baseDirectory, string version, List<string> widgets)
    {
        string versionPath = System.IO.Path.Combine(baseDirectory, version);
        if (!Directory.Exists(versionPath))
        {
            Directory.CreateDirectory(versionPath);
        }

        List<string> userWidgets = new List<string>();
        Console.WriteLine("Enter additional widget names for version " + version + " (type 'done' to finish):");

        while (true)
        {
            string widgetInput = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(widgetInput) || widgetInput.ToLower() == "done") break;
            userWidgets.Add(widgetInput);
        }

        Console.WriteLine("Version folder created: " + versionPath);
        Console.WriteLine("Widgets included: " + string.Join(", ", widgets.Concat(userWidgets)));
    }

    static void GenerateTXTFileForWidgets(string pathtxt, string baseDirectory, string version, List<string> widgets)
    {
        foreach (var widget in widgets)
        {
            string widgetDirectory = System.IO.Path.Combine("../../../Reports", version, widget);
            string versionpath = System.IO.Path.Combine("../../../ControlIndex", version);
            List<string> txtFiles = new List<string>();

            if (Directory.Exists(widgetDirectory))
            {
                txtFiles.AddRange(Directory.GetFiles(widgetDirectory, "*.txt"));
            }

            if (txtFiles.Count == 0)
            {
                Console.WriteLine($"No TXT files found for widget: {widget}. Skipping...");
                continue;
            }

            int totalTests = 0, passedTests = 0, failedTests = 0;
            double totalDuration = 0;
            var widgetResults = new Dictionary<string, (int total, int passed, int failed)>();

            string filePath = System.IO.Path.Combine("../../../Reports", version, widget, $"{widget}.txt"); // Update with actual path

            try
            {
                DateTime startTime = DateTime.MinValue, endTime = DateTime.MinValue;

                string[] logLines = File.ReadAllLines(filePath);
                Regex testPattern = new Regex(@"(\d{2}:\d{2}) \+(\d+)(?: -(\d+))?");

                foreach (string line in logLines)
                {
                    Match match = testPattern.Match(line);
                    if (match.Success)
                    {
                        if (startTime == DateTime.MinValue)
                            startTime = ConvertToDateTime(match.Groups[1].Value); // DateTime.ParseExact(match.Groups[1].Value, "mm:ss", null);

                        endTime = ConvertToDateTime(match.Groups[1].Value); ;
                        totalTests = int.Parse(match.Groups[2].Value);
                        failedTests = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                        string[] parts = line.Split("dart: ");
                        if (parts.Length > 1)
                        {
                            string testName = parts[1].Trim();
                            if (!widgetResults.ContainsKey(testName))
                                widgetResults[testName] = (0, 0, 0);

                            var (total, passed, failed) = widgetResults[testName];
                            total++;

                            if (line.Contains("[E]")) failed++;
                            else passed++;

                            widgetResults[testName] = (total, passed, failed);
                        }
                    }
                }

                passedTests = totalTests - failedTests;
                widgetver[widget] = (totalTests, passedTests, failedTests);
                if (startTime != null && endTime != null)
                    totalDuration = (endTime - startTime).TotalSeconds;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            }

            string htmlPage = widget + ".html";
            string outputFilePath = System.IO.Path.Combine(baseDirectory, version, htmlPage);
            try
            {
                CreateVersionIndex(versionpath, version, widgets, widgetver);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to create widget index HTML" + ex.ToString());
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputFilePath) ?? baseDirectory);
            File.WriteAllText(outputFilePath, GenerateHtmlReport(totalTests, passedTests, failedTests, totalDuration, widgetResults));

            Console.WriteLine($"HTML summary generated for widget {widget}: {outputFilePath}");

        }
    }

    public static DateTime ConvertToDateTime(string timeString)
    {
        // Split the time string into parts
        var parts = timeString.Split(':');

        // Parse minutes and seconds
        int minutes = int.Parse(parts[0]);
        int seconds = int.Parse(parts[1]);

        // Calculate hours and correct minutes
        int hours = minutes / 60;
        minutes = minutes % 60;

        // Create a DateTime object for today's date at the calculated time
        DateTime dateTime = DateTime.Today.AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);

        return dateTime;
    }

    static void CreateVersionIndex(string versionPath, string version, List<string> widgets, Dictionary<string, (int total, int pass, int fail)> widgetResults)
    {
        StringBuilder htmlcontent = new StringBuilder();
        htmlcontent.AppendLine("<!DOCTYPE html>");
        htmlcontent.AppendLine("<html lang=\"en\">");
        htmlcontent.AppendLine("<head>");
        htmlcontent.AppendLine(" <meta charset=\"UTF-8\">");
        htmlcontent.AppendLine(" <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        htmlcontent.AppendLine(" <title>Flutter Syncfusion Widgets - Test Reports</title>");
        htmlcontent.AppendLine(" <style>");
        htmlcontent.AppendLine(" body { font-family: Arial, sans-serif; margin: 20px; background-color: #f4f4f4; }");
        htmlcontent.AppendLine(" table { width: 100%; border-collapse: collapse; margin-top: 20px; background: white; }");
        htmlcontent.AppendLine(" th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        htmlcontent.AppendLine(" th { background-color: #007bff; color: white; }");
        htmlcontent.AppendLine(" .passed { color: green; font-weight: bold; }");
        htmlcontent.AppendLine(" .failed { color: red; font-weight: bold; }");
        htmlcontent.AppendLine(" a { text-decoration: none; color: #007bff; font-weight: bold; }");
        htmlcontent.AppendLine(" a:hover { text-decoration: underline; }");
        htmlcontent.AppendLine(" </style>");
        htmlcontent.AppendLine("</head>");
        htmlcontent.AppendLine("<body>");
        htmlcontent.AppendLine($" <h2 style=\"text-align: center;\">Flutter Syncfusion Widgets - Test Reports (Version {version})</h2>");
        htmlcontent.AppendLine(" <h3>Test Summary</h3>");
        htmlcontent.AppendLine(" <table>");
        htmlcontent.AppendLine(" <thead>");
        htmlcontent.AppendLine(" <tr>");
        htmlcontent.AppendLine(" <th>Widget Name</th>");
        htmlcontent.AppendLine(" <th>Test Cases</th>");
        htmlcontent.AppendLine(" <th>Passed</th>");
        htmlcontent.AppendLine(" <th>Failed</th>");
        htmlcontent.AppendLine(" </tr>");
        htmlcontent.AppendLine(" </thead>");
        htmlcontent.AppendLine(" <tbody>");
        foreach (var widget in widgets)
        {
            var (total, pass, fail) = widgetResults.ContainsKey(widget) ? widgetResults[widget] : (0, 0, 0);
            htmlcontent.AppendLine($@"
        <tr>
            <td><a href='{widget}/{widget}.html'>{widget}</a></td>
            <td>{total}</td>
            <td class='passed'>{pass}</td>
            <td class='failed'>{fail}</td>
        </tr>");
        }
        htmlcontent.AppendLine(" </tbody>");
        htmlcontent.AppendLine(" </table>");
        htmlcontent.AppendLine("</body>");
        htmlcontent.AppendLine("</html>");
        File.WriteAllText(System.IO.Path.Combine(versionPath, "index.html"), htmlcontent.ToString());
    }

    static string GenerateHtmlReport(int totalTests, int passedTests, int failedTests, double totalDuration, Dictionary<string, (int total, int passed, int failed)> widgetResults)
    {

        string htmlContent = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <title>Test Results Summary</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; background-color: #f8f9fa; }}
        table {{ width: 100%; border-collapse: collapse; background: white; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #007bff; color: white; }}
        .passed {{ color: green; }} 
        .failed {{ color: red; }}
    </style>
</head>
<body>
    <h2>Test Results Summary</h2>
    <p>Total Tests: {totalTests}</p>
    <p>Passed: <span class='passed'>{passedTests}</span></p>
    <p>Failed: <span class='failed'>{failedTests}</span></p>
    <p>Execution Time: {totalDuration:F2} seconds</p>
    <h3>Summary</h3>
    <table>
        <tr>
            <th>Test Name</th><th>Total</th><th>Passed</th><th>Failed</th>
        </tr>";

        foreach (var widget in widgetResults)
        {
            htmlContent += $"<tr><td>{widget.Key}</td><td>{widget.Value.total}</td><td class='passed'>{widget.Value.passed}</td><td class='failed'>{widget.Value.failed}</td></tr>\n";
        }

        htmlContent += "    </table>\n</body>\n</html>";

        return htmlContent;
    }

static Dictionary<string, (List<(string testName, bool isPassed)>, int total, int passed, int failed)> ParseTestFiles(string filePath)
    {
        var testGroups = new Dictionary<string, (List<(string testName, bool isPassed)>, int total, int passed, int failed)>();
        string currentGroup = null;
        foreach (var line in File.ReadAllLines(filePath))
        {
            // Check if the line is denoting a test group
            if (line.Contains("Group:"))
            {
                // Extract and set the current group name from the line
                int startIndex = line.IndexOf("Group:") + 6;
                int endIndex = line.IndexOf(':', startIndex);
                currentGroup = line.Substring(startIndex, endIndex - startIndex).Trim();

                // Initialize the group in the dictionary if it doesn't already exist
                if (!testGroups.ContainsKey(currentGroup))
                {
                    testGroups[currentGroup] = (new List<(string, bool)>(), 0, 0, 0);
                }
            }

            // Skip lines that do not contain the group label
            if (currentGroup == null) continue;

            // Extract test case name
            string testName = line.Substring(line.LastIndexOf(":") + 1).Trim();

            // Check for test results and update counts
            bool isPassed = !line.Contains("[E]");
            var (testCases, total, passed, failed) = testGroups[currentGroup];
            if (line.Contains("Group:")) {
                testCases.Add((testName, isPassed));
            }
            total++;
            if (isPassed)
                passed++;
            else
                failed++;

            // Update the dictionary with the current counts
            testGroups[currentGroup] = (testCases, total, passed, failed);
        }

        return testGroups;
    }

    // New Method: GenerateTestReportHtml
    static void GenerateGroupHtmlReports(Dictionary<string, (List<(string testName, bool isPassed)> testCases, int total, int passed, int failed)> groupTests, string baseDirectory, string version, string widget)
    {
        string groupReportsDirectory = System.IO.Path.Combine(baseDirectory, version, widget);
        Directory.CreateDirectory(groupReportsDirectory);

        foreach (var (group, (tests, total, passed, failed)) in groupTests)
        {
            var groupReport = new StringBuilder();
            groupReport.AppendLine("<!DOCTYPE html>");
            groupReport.AppendLine("<html lang=\"en\">");
            groupReport.AppendLine("<head>");
            groupReport.AppendLine("    <meta charset='UTF-8'>");
            groupReport.AppendLine($"    <title>{group} Test Report</title>");
            groupReport.AppendLine("    <style>");
            groupReport.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f8f9fa; }");
            groupReport.AppendLine("        table { width: 100%; border-collapse: collapse; background: white; }");
            groupReport.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            groupReport.AppendLine("        th { background-color: #007bff; color: white; }");
            groupReport.AppendLine("        .passed { color: green; font-weight: bold; }");
            groupReport.AppendLine("        .failed { color: red; font-weight: bold; }");
            groupReport.AppendLine("    </style>");
            groupReport.AppendLine("</head>");
            groupReport.AppendLine("<body>");
            groupReport.AppendLine($"    <h2>{group} Test Cases</h2>");
            groupReport.AppendLine($"    <p>Total Tests: {total}, Passed: {passed}, Failed: {failed}</p>");
            groupReport.AppendLine("    <table>");
            groupReport.AppendLine("        <thead><tr><th>Test Case</th><th>Status</th></tr></thead>");
            groupReport.AppendLine("        <tbody>");

            foreach (var (testName, isPassed) in tests)
            {
                string status = isPassed ? "Passed" : "Failed";
                string classAttribute = isPassed ? "passed" : "failed";
                groupReport.AppendLine($"        <tr><td>{testName}</td><td class='{classAttribute}'>{status}</td></tr>");
            }

            groupReport.AppendLine("        </tbody>");
            groupReport.AppendLine("    </table>");
            groupReport.AppendLine("</body>");
            groupReport.AppendLine("</html>");

            string groupReportFilePath = System.IO.Path.Combine(groupReportsDirectory, $"{group}.html");
            File.WriteAllText(groupReportFilePath, groupReport.ToString());
        }
    }

static void GenerateSummaryHtml(Dictionary<string, (int total, int passed, int failed)> groupResults,
                                string outputPath, string version)
    {
        var summaryContent = new StringBuilder();
        summaryContent.AppendLine("<!DOCTYPE html>");
        summaryContent.AppendLine("<html lang=\"en\">");
        summaryContent.AppendLine("<head>");
        summaryContent.AppendLine("    <meta charset=\"UTF-8\">");
        summaryContent.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        summaryContent.AppendLine($"    <title>Test Summary (Version: {version})</title>");
        summaryContent.AppendLine("    <style>");
        summaryContent.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        summaryContent.AppendLine("        h2 { text-align: center; }");
        summaryContent.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
        summaryContent.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        summaryContent.AppendLine("        th { background-color: #007bff; color: white; }");
        summaryContent.AppendLine("        .passed { color: green; }");
        summaryContent.AppendLine("        .failed { color: red; }");
        summaryContent.AppendLine("        .link a { color: #007bff; text-decoration: none; }");
        summaryContent.AppendLine("        a:hover { text-decoration: underline; }");
        summaryContent.AppendLine("    </style>");
        summaryContent.AppendLine("</head>");
        summaryContent.AppendLine("<body>");
        summaryContent.AppendLine("    <h2>Test Summary</h2>");
        summaryContent.AppendLine("    <table>");
        summaryContent.AppendLine("        <thead>");
        summaryContent.AppendLine("            <tr>");
        summaryContent.AppendLine("                <th>Test Group</th>");
        summaryContent.AppendLine("                <th>Total</th>");
        summaryContent.AppendLine("                <th>Passed</th>");
        summaryContent.AppendLine("                <th>Failed</th>");
        summaryContent.AppendLine("                <th>Details</th>");
        summaryContent.AppendLine("            </tr>");
        summaryContent.AppendLine("        </thead>");
        summaryContent.AppendLine("        <tbody>");

        foreach (var (groupName, results) in groupResults)
        {
            summaryContent.AppendLine($"        <tr>");
            summaryContent.AppendLine($"            <td>{groupName}</td>");
            summaryContent.AppendLine($"            <td>{results.total}</td>");
            summaryContent.AppendLine($"            <td class='passed'>{results.passed}</td>");
            summaryContent.AppendLine($"            <td class='failed'>{results.failed}</td>");
            summaryContent.AppendLine($"            <td class='link'><a href='{groupName}.html'>View Details</a></td>");
            summaryContent.AppendLine($"        </tr>");
        }

        summaryContent.AppendLine("        </tbody>");
        summaryContent.AppendLine("    </table>");
        summaryContent.AppendLine("</body>");
        summaryContent.AppendLine("</html>");        
        File.WriteAllText(outputPath, summaryContent.ToString());
    }
    static void GenerateIndexHtml(List<string> versions, string filePath)
    {
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("<!DOCTYPE html>");
            writer.WriteLine("<html lang=\"en\">");
            writer.WriteLine("<head>");
            writer.WriteLine("    <meta charset=\"UTF-8\">");
            writer.WriteLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            writer.WriteLine("    <title>Syncfusion Test Results</title>");
            writer.WriteLine("    <style>");
            writer.WriteLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f4f4f4; }");
            writer.WriteLine("        h2 { text-align: center; }");
            writer.WriteLine("        ul { list-style-type: none; padding: 0; text-align: center; }");
            writer.WriteLine("        li { margin: 10px 0; }");
            writer.WriteLine("        a { text-decoration: none; color: #007bff; font-size: 18px; font-weight: bold; }");
            writer.WriteLine("        a:hover { text-decoration: underline; }");
            writer.WriteLine("    </style>");
            writer.WriteLine("</head>");
            writer.WriteLine("<body>");
            writer.WriteLine("    <h2>Syncfusion Test Results</h2>");
            writer.WriteLine("    <h3 style=\"text-align: center;\">Versions</h3>");
            writer.WriteLine("    <ul>");

            foreach (var version in versions)
            {
                writer.WriteLine($"        <li><a href='ControlIndex/{version}/index.html'>{version}</a></li>");
            }

            writer.WriteLine("    </ul>");
            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
        }
    }

    class FlutterTestRunner
    {
        public static void Run()
        {
            string[] repositories = {
                ""
        };

            foreach (var repoUrl in repositories)
            {
                string urlWithToken = repoUrl.Insert(8, $""); // Inserts the token at the right place in the URL
                HandleRepository(urlWithToken);
            }
        }

        private static void HandleRepository(string repositoryUrl)
        {
            string repoName = GetRepoName(repositoryUrl);
            string cloneDirectory = $"repos/{repoName}";

            // Step 1: Clone the repository
            Console.WriteLine($"Cloning {repositoryUrl}...");
            RunCommand("git", $"clone {repositoryUrl} {cloneDirectory}");

            // Step 2: Change the working directory to the cloned directory
            string clonePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), cloneDirectory);

            // Step 3: Run Flutter tests
            Console.WriteLine($"Running Flutter tests for {repoName}...");
            RunCommand("flutter", "test", clonePath);

            // Step 4: Generate test report
            string reportDirectory = System.IO.Path.Combine(clonePath, "test_reports");
            Directory.CreateDirectory(reportDirectory);
            string reportFilePath = System.IO.Path.Combine(reportDirectory, "test_report.txt");

            Console.WriteLine($"Writing test report to {reportFilePath}...");
            File.WriteAllText(reportFilePath, "Test report content goes here.");

            Console.WriteLine($"Flutter tests completed for {repoName}.");
        }

        private static string GetRepoName(string repositoryUrl)
        {
            return repositoryUrl.Split('/')[^1].Replace(".git", "");
        }

        private static void RunCommand(string command, string arguments, string workingDirectory = null)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
            };

            using (Process process = new Process { StartInfo = processInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Console.WriteLine(output);
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine("Errors:");
                    Console.WriteLine(error);
                }
            }
        }
    }
}
