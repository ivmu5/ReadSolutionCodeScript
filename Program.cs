using System.Diagnostics;
using System.Text;

internal static class Program
{
    private static readonly HashSet<string> ExcludedDirectories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "bin",
            "obj",
            ".git",
            ".vs"
        };

    private static int Main(string[] args)
    {
        string? projectPath = GetProjectPath(args);

        if (projectPath is null)
        {
            PrintError("Invalid project path.");
            return 1;
        }

        var stopwatch = Stopwatch.StartNew();

        string projectName = new DirectoryInfo(projectPath).Name;
        string dateTime = DateTime.Now.ToString("yyMMdd_HHmm");

        string desktopPath = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);

        string outputFile = Path.Combine(
            desktopPath,
            $"{projectName}_{dateTime}.txt");

        PrintHeader(
            projectName,
            projectPath,
            outputFile);

        Console.WriteLine("Scanning project...");

        List<string> allCsFiles;

        try
        {
            allCsFiles = Directory
                .EnumerateFiles(
                    projectPath,
                    "*.cs",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true
                    })
                .ToList();
        }
        catch (Exception ex)
        {
            PrintError(
                $"Failed to scan project: {SanitizeError(ex.Message, projectPath)}");

            return 1;
        }

        List<string> csFiles = allCsFiles
            .Where(file => !IsExcluded(projectPath, file))
            .OrderBy(
                file => Path.GetRelativePath(projectPath, file),
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        int excludedFiles = allCsFiles.Count - csFiles.Count;

        Console.WriteLine($"Found    : {allCsFiles.Count:N0} .cs files");
        Console.WriteLine($"Excluded : {excludedFiles:N0} files");
        Console.WriteLine($"To export: {csFiles.Count:N0} files");
        Console.WriteLine();

        long totalSourceLines = 0;
        long totalCodeLines = 0;
        long totalEmptyLines = 0;
        long totalSourceBytes = 0;
        long outputLinesWritten = 0;

        int processedFiles = 0;
        int failedFiles = 0;

        var errors = new List<string>();

        try
        {
            using var writer = new StreamWriter(
                outputFile,
                false,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            for (int i = 0; i < csFiles.Count; i++)
            {
                string file = csFiles[i];

                string relativePath = Path.GetRelativePath(
                    projectPath,
                    file);

                try
                {
                    string[] lines = File.ReadAllLines(file);

                    long codeLines = lines.Count(
                        line => !string.IsNullOrWhiteSpace(line));

                    long emptyLines =
                        lines.Length - codeLines;

                    totalSourceLines += lines.Length;
                    totalCodeLines += codeLines;
                    totalEmptyLines += emptyLines;
                    totalSourceBytes += new FileInfo(file).Length;

                    writer.WriteLine(
                        "====================================================");

                    writer.WriteLine(relativePath);

                    writer.WriteLine(
                        "====================================================");

                    foreach (string line in lines)
                    {
                        writer.WriteLine(line);
                    }

                    writer.WriteLine();

                    outputLinesWritten += lines.Length + 4;
                    processedFiles++;
                }
                catch (Exception ex)
                {
                    failedFiles++;

                    string message = SanitizeError(
                        ex.Message,
                        projectPath);

                    string error =
                        $"{relativePath}: {message}";

                    errors.Add(error);

                    writer.WriteLine(
                        "====================================================");

                    writer.WriteLine(
                        $"ERROR: {error}");

                    writer.WriteLine(
                        "====================================================");

                    writer.WriteLine();

                    outputLinesWritten += 4;
                }

                PrintProgress(
                    i + 1,
                    csFiles.Count,
                    relativePath,
                    stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();

            PrintError(
                $"Failed to create output file: {SanitizeError(ex.Message, projectPath)}");

            return 1;
        }

        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine();

        var outputInfo = new FileInfo(outputFile);

        double averageLines = processedFiles > 0
            ? (double)totalSourceLines / processedFiles
            : 0;

        PrintSummary(
            projectName,
            processedFiles,
            excludedFiles,
            failedFiles,
            totalSourceLines,
            totalCodeLines,
            totalEmptyLines,
            outputLinesWritten,
            averageLines,
            totalSourceBytes,
            outputInfo.Length,
            stopwatch.Elapsed,
            outputFile,
            errors);

        return failedFiles == 0 ? 0 : 2;
    }

    private static string? GetProjectPath(string[] args)
    {
        if (args.Length > 0)
        {
            string? argumentPath = NormalizePath(args[0]);

            if (argumentPath is null)
            {
                PrintError("Invalid project path.");
                return null;
            }

            if (!Directory.Exists(argumentPath))
            {
                PrintError(
                    $"Directory does not exist: {argumentPath}");

                return null;
            }

            return argumentPath;
        }

        Console.WriteLine("C# Project Export");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Enter project directory: ");

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                PrintError("Path cannot be empty.");
                Console.WriteLine();
                continue;
            }

            string? projectPath = NormalizePath(input);

            if (projectPath is null)
            {
                PrintError("Invalid path.");
                Console.WriteLine();
                continue;
            }

            if (!Directory.Exists(projectPath))
            {
                PrintError("Directory does not exist.");
                Console.WriteLine();
                continue;
            }

            return projectPath;
        }
    }

    private static string? NormalizePath(string path)
    {
        path = path.Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsExcluded(
        string projectPath,
        string filePath)
    {
        string relativePath = Path.GetRelativePath(
            projectPath,
            filePath);

        string? directory =
            Path.GetDirectoryName(relativePath);

        if (string.IsNullOrEmpty(directory))
            return false;

        return directory
            .Split(
                [
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(ExcludedDirectories.Contains);
    }

    private static string SanitizeError(
        string message,
        string projectPath)
    {
        return message.Replace(
            projectPath,
            ".",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintHeader(
        string projectName,
        string projectPath,
        string outputFile)
    {
        Console.WriteLine(
            "====================================================");

        Console.WriteLine(" C# PROJECT EXPORT");

        Console.WriteLine(
            "====================================================");

        Console.WriteLine($"Project : {projectName}");
        Console.WriteLine($"Source  : {projectPath}");
        Console.WriteLine($"Output  : {outputFile}");
        Console.WriteLine();
    }

    private static void PrintSummary(
        string projectName,
        int processedFiles,
        int excludedFiles,
        int failedFiles,
        long totalSourceLines,
        long totalCodeLines,
        long totalEmptyLines,
        long outputLinesWritten,
        double averageLines,
        long totalSourceBytes,
        long outputSize,
        TimeSpan elapsed,
        string outputFile,
        IReadOnlyCollection<string> errors)
    {
        Console.ForegroundColor =
            ConsoleColor.Green;

        Console.WriteLine(
            "====================================================");

        Console.WriteLine(" COMPLETED");

        Console.WriteLine(
            "====================================================");

        Console.ResetColor();

        Console.WriteLine();

        Console.WriteLine(
            $"Project               : {projectName}");

        Console.WriteLine(
            $"Processed files       : {processedFiles:N0}");

        Console.WriteLine(
            $"Excluded files        : {excludedFiles:N0}");

        Console.WriteLine(
            $"Failed files          : {failedFiles:N0}");

        Console.WriteLine();

        Console.WriteLine(
            $"Source lines total    : {totalSourceLines:N0}");

        Console.WriteLine(
            $"Code lines (non-empty): {totalCodeLines:N0}");

        Console.WriteLine(
            $"Empty lines           : {totalEmptyLines:N0}");

        Console.WriteLine(
            $"Output file lines     : {outputLinesWritten:N0}");

        Console.WriteLine();

        Console.WriteLine(
            $"Avg lines per file    : {averageLines:N1}");

        Console.WriteLine(
            $"Source size           : {FormatBytes(totalSourceBytes)}");

        Console.WriteLine(
            $"Output size           : {FormatBytes(outputSize)}");

        Console.WriteLine();

        Console.WriteLine(
            $"Execution time        : {FormatTime(elapsed)}");

        Console.WriteLine(
            $"Output file           : {outputFile}");

        if (errors.Count > 0)
        {
            Console.ForegroundColor =
                ConsoleColor.Yellow;

            Console.WriteLine();
            Console.WriteLine("Errors:");

            foreach (string error in errors)
            {
                Console.WriteLine($" - {error}");
            }

            Console.ResetColor();
        }

        Console.WriteLine();

        Console.WriteLine(
            "====================================================");
    }

    private static void PrintProgress(
        int current,
        int total,
        string currentFile,
        TimeSpan elapsed)
    {
        const int barLength = 30;

        double progress = total > 0
            ? (double)current / total
            : 1;

        int completed =
            (int)(progress * barLength);

        string bar =
            new string('#', completed) +
            new string(
                '-',
                barLength - completed);

        string fileName =
            Path.GetFileName(currentFile);

        if (fileName.Length > 35)
        {
            fileName =
                fileName[..32] + "...";
        }

        double percent =
            progress * 100;

        string text =
            $"[{bar}] " +
            $"{percent,6:F2}% " +
            $"[{current}/{total}] " +
            $"{fileName,-35} " +
            $"Elapsed: {FormatTime(elapsed)}";

        int width;

        try
        {
            width = Math.Max(
                Console.WindowWidth - 1,
                1);
        }
        catch
        {
            width = 120;
        }

        if (text.Length > width)
        {
            text = text[..width];
        }

        Console.Write(
            "\r" + text.PadRight(width));
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor =
            ConsoleColor.Red;

        Console.WriteLine(message);

        Console.ResetColor();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units =
        [
            "B",
            "KB",
            "MB",
            "GB",
            "TB"
        ];

        double size = bytes;
        int unit = 0;

        while (
            size >= 1024 &&
            unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:N2} {units[unit]}";
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return
                $"{(int)time.TotalHours:00}:" +
                $"{time.Minutes:00}:" +
                $"{time.Seconds:00}." +
                $"{time.Milliseconds:000}";
        }

        if (time.TotalMinutes >= 1)
        {
            return
                $"{time.Minutes:00}:" +
                $"{time.Seconds:00}." +
                $"{time.Milliseconds:000}";
        }

        return $"{time.TotalSeconds:F3} sec";
    }
}