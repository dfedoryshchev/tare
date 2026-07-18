using System.Text.Json;
using Tare.Cli;
using Tare.Core;

// Minimal arg handling for now; a proper command surface (System.CommandLine)
// arrives once there is more than one verb to route (bench, S10).
if (args.Length < 2 || args[0] != "analyze")
{
    Console.Error.WriteLine("usage: tare analyze <file> [--json] [--config <path>]");
    return 1;
}

var json = args.Contains("--json");
var path = FirstInputPath(args);
if (path is null)
{
    Console.Error.WriteLine("usage: tare analyze <file> [--json] [--config <path>]");
    return 1;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"error: file not found: {path}");
    return 1;
}

// Config resolution: an explicit --config wins; otherwise a tare.json beside the working
// directory is picked up automatically; otherwise the calibrated defaults apply.
var configPath = ConfigOption(args) ?? (File.Exists("tare.json") ? "tare.json" : null);
TareOptions options;
try
{
    options = configPath is null ? TareOptions.Default : TareOptions.FromJson(File.ReadAllText(configPath));
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"error: invalid config {configPath}: {ex.Message}");
    return 1;
}

var source = File.ReadAllText(path);
var result = Analyzer.Analyze(source, options);
Console.Write(json ? JsonReport.Serialize(result) + "\n" : Reporter.Render(path, result));

return 0;

// Returns the value passed to --config, or null if the flag is absent.
static string? ConfigOption(string[] args)
{
    var i = Array.IndexOf(args, "--config");
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

// The input file is the first bare argument (after the verb) that is neither a flag nor the
// value consumed by --config.
static string? FirstInputPath(string[] args)
{
    var configValue = ConfigOption(args);
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i].StartsWith("--") || args[i] == configValue)
        {
            continue;
        }

        return args[i];
    }

    return null;
}
