using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;

class Program
{
    public static List<string> widgets
    {
        get
        {
            return new List<string>()
            {
                "SfChat",
                "SfAIAssistView"
            };
        }
    }
    public static Dictionary<string, (int total, int passed, int failed)> widgetver =
    new Dictionary<string, (int, int, int)>();
    public static List<string> versions = new List<string>();
    static void Main(string[] args)
    {
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
        string versionPath = Path.Combine(baseDirectory, version);
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
            string widgetDirectory = Path.Combine("../../../Reports", version, widget);
            string versionpath = Path.Combine("../../../ControlIndex", version);
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

            string filePath = Path.Combine("../../../Reports", version, widget, $"{widget}.txt"); // Update with actual path

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
                            startTime = DateTime.ParseExact(match.Groups[1].Value, "mm:ss", null);

                        endTime = DateTime.ParseExact(match.Groups[1].Value, "mm:ss", null);
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
            string outputFilePath = Path.Combine(baseDirectory, version, htmlPage);
            try
            {
                CreateVersionIndex(versionpath, version, widgets, widgetver);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to create widget index HTML" + ex.ToString());
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? baseDirectory);
            File.WriteAllText(outputFilePath, GenerateHtmlReport(totalTests, passedTests, failedTests, totalDuration, widgetResults));

            Console.WriteLine($"HTML summary generated for widget {widget}: {outputFilePath}");

        }
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
            <td><a href='{widget}.html'>{widget}</a></td>
            <td>{total}</td>
            <td class='passed'>{pass}</td>
            <td class='failed'>{fail}</td>
        </tr>");
        }
        htmlcontent.AppendLine(" </tbody>");
        htmlcontent.AppendLine(" </table>");
        htmlcontent.AppendLine("</body>");
        htmlcontent.AppendLine("</html>");
        File.WriteAllText(Path.Combine(versionPath, "index.html"), htmlcontent.ToString());
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
}
