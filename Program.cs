using System.Diagnostics;
using System.Text;

class Program
{
    static void Main()
    {
        const string filePath = @"";

        if (!Directory.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Invalid path: {filePath}");
            Console.ResetColor();
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        // Имя проекта = последняя папка в пути.
        string projectName = new DirectoryInfo(filePath).Name;

        // Например: SQLiteStorage_260916_1125.txt
        string dateTime = DateTime.Now.ToString("yyMMdd_HHmm");

        string desktopPath = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);

        string outputFile = Path.Combine(
            desktopPath,
            $"{projectName}_{dateTime}.txt");

        Console.WriteLine("====================================================");
        Console.WriteLine(" C# PROJECT EXPORT");
        Console.WriteLine("====================================================");
        Console.WriteLine($"Project : {projectName}");
        Console.WriteLine($"Source  : {filePath}");
        Console.WriteLine($"Output  : {outputFile}");
        Console.WriteLine();

        Console.WriteLine("Scanning project...");

        var allCsFiles = Directory
            .EnumerateFiles(
                filePath,
                "*.cs",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true
                })
            .ToList();

        var csFiles = allCsFiles
            .Where(file => !IsExcluded(file))
            .ToList();

        int excludedFiles = allCsFiles.Count - csFiles.Count;

        Console.WriteLine($"Found    : {allCsFiles.Count:N0} .cs files");
        Console.WriteLine($"Excluded : {excludedFiles:N0} files (bin/obj)");
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

        // UTF-8 без BOM.
        using (var writer = new StreamWriter(
                   outputFile,
                   false,
                   new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            for (int i = 0; i < csFiles.Count; i++)
            {
                string file = csFiles[i];

                try
                {
                    string[] lines = File.ReadAllLines(file);

                    long codeLines = lines.Count(line =>
                        !string.IsNullOrWhiteSpace(line));

                    long emptyLines = lines.Length - codeLines;

                    totalSourceLines += lines.Length;
                    totalCodeLines += codeLines;
                    totalEmptyLines += emptyLines;
                    totalSourceBytes += new FileInfo(file).Length;

                    writer.WriteLine("====================================================");
                    writer.WriteLine(file);
                    writer.WriteLine("====================================================");

                    foreach (string line in lines)
                        writer.WriteLine(line);

                    writer.WriteLine();

                    // 3 строки заголовка + исходник + пустая строка.
                    outputLinesWritten += lines.Length + 4;

                    processedFiles++;
                }
                catch (Exception ex)
                {
                    failedFiles++;

                    string error = $"{file}: {ex.Message}";
                    errors.Add(error);

                    writer.WriteLine("====================================================");
                    writer.WriteLine($"ERROR: {error}");
                    writer.WriteLine("====================================================");
                    writer.WriteLine();

                    outputLinesWritten += 4;
                }

                PrintProgress(
                    i + 1,
                    csFiles.Count,
                    file,
                    stopwatch.Elapsed);
            }
        }

        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine();

        var outputInfo = new FileInfo(outputFile);

        double averageLines = processedFiles > 0
            ? (double)totalSourceLines / processedFiles
            : 0;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("====================================================");
        Console.WriteLine(" COMPLETED");
        Console.WriteLine("====================================================");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine($"Project              : {projectName}");
        Console.WriteLine($"Processed files      : {processedFiles:N0}");
        Console.WriteLine($"Excluded files       : {excludedFiles:N0}");
        Console.WriteLine($"Failed files         : {failedFiles:N0}");
        Console.WriteLine();

        Console.WriteLine($"Source lines total   : {totalSourceLines:N0}");
        Console.WriteLine($"Code lines (non-empty): {totalCodeLines:N0}");
        Console.WriteLine($"Empty lines          : {totalEmptyLines:N0}");
        Console.WriteLine($"Output file lines    : {outputLinesWritten:N0}");
        Console.WriteLine();

        Console.WriteLine($"Avg lines per file   : {averageLines:N1}");
        Console.WriteLine($"Source size          : {FormatBytes(totalSourceBytes)}");
        Console.WriteLine($"Output size          : {FormatBytes(outputInfo.Length)}");
        Console.WriteLine();

        Console.WriteLine($"Execution time       : {FormatTime(stopwatch.Elapsed)}");
        Console.WriteLine($"Output file          : {outputFile}");

        if (errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine();
            Console.WriteLine("Errors:");

            foreach (string error in errors)
                Console.WriteLine($" - {error}");

            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("====================================================");
    }

    private static bool IsExcluded(string path)
    {
        string[] parts = path.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("obj", StringComparison.OrdinalIgnoreCase));
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

        int completed = (int)(progress * barLength);

        string bar =
            new string('#', completed) +
            new string('-', barLength - completed);

        string fileName = Path.GetFileName(currentFile);

        if (fileName.Length > 35)
            fileName = fileName[..32] + "...";

        double percent = progress * 100;

        string text =
            $"[{bar}] " +
            $"{percent,6:F2}% " +
            $"[{current}/{total}] " +
            $"{fileName,-35} " +
            $"Elapsed: {FormatTime(elapsed)}";

        // Записываем поверх предыдущей строки.
        int width = Math.Max(Console.WindowWidth - 1, 1);

        if (text.Length > width)
            text = text[..width];

        Console.Write("\r" + text.PadRight(width));
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];

        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:N2} {units[unit]}";
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";

        if (time.TotalMinutes >= 1)
            return $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";

        return $"{time.TotalSeconds:F3} sec";
    }
}