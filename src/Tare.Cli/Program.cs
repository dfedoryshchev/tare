using Tare.Cli;
using Tare.Core;

// Minimal arg handling for now; a proper command surface (System.CommandLine)
// arrives once there is more than one verb to route.
if (args.Length < 2 || args[0] != "analyze")
{
    Console.Error.WriteLine("usage: tare analyze <file>");
    return 1;
}

var path = args[1];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"error: file not found: {path}");
    return 1;
}

var source = File.ReadAllText(path);
var result = Analyzer.Analyze(source);
Console.Write(Reporter.Render(path, result));

return 0;
