using System.CommandLine;
using System.Text.Json;
using Tare.Cli;
using Tare.Core;
using Tare.Http;

// Two verbs now, so the hand-rolled arg walk from the analyze-only days is gone.
// System.CommandLine owns routing, help and error text; everything below is glue between the
// parsed values and Tare.Core, which still does no IO of its own.
//
// This is also where the adapters get built. Tare.Core names the citation questions and never
// learns who answers them, so choosing an answerer is a front-end decision and lives here.

var fileArgument = new Argument<FileInfo>("file")
{
    Description = "the markdown document to analyze",
};

var jsonOption = new Option<bool>("--json")
{
    Description = "emit the report as JSON instead of console text",
};

var configOption = new Option<FileInfo?>("--config")
{
    Description = "path to a tare.json; defaults to one in the working directory if present",
};

var verifyOption = new Option<bool>("--verify")
{
    Description = "also read each cited source and ask whether it backs the claim it is cited "
                  + "for; needs ANTHROPIC_API_KEY, costs one model call per cited claim, and "
                  + "never changes the score",
};

var corpusOption = new Option<DirectoryInfo>("--corpus")
{
    Description = "the labeled corpus directory (expects manifest.json and cases/)",
    DefaultValueFactory = _ => new DirectoryInfo("corpus"),
};

var analyze = new Command("analyze", "Score a document and report its findings")
{
    fileArgument,
    jsonOption,
    configOption,
    verifyOption,
};

analyze.SetAction(async (parse, cancellationToken) =>
{
    var file = parse.GetValue(fileArgument)!;
    if (!file.Exists)
    {
        Console.Error.WriteLine($"error: file not found: {file.FullName}");
        return 1;
    }

    if (!TryLoadOptions(parse.GetValue(configOption), out var options))
    {
        return 1;
    }

    var source = File.ReadAllText(file.FullName);
    var result = parse.GetValue(verifyOption)
        ? await Analyzer.AnalyzeAsync(source, Verifier(), options, cancellationToken)
        : Analyzer.Analyze(source, options);

    Console.Write(parse.GetValue(jsonOption)
        ? JsonReport.Serialize(result) + "\n"
        : Reporter.Render(file.Name, result));
    return 0;
});

// No --verify here, and it is not an omission. Bench measures the deterministic rules against
// hand-read labels; an answer that varies between runs, costs money and has no labeled set
// behind it would make the precision number mean nothing.
var bench = new Command("bench", "Score the analyzer against the labeled corpus")
{
    corpusOption,
    configOption,
};

bench.SetAction(parse =>
{
    var corpus = parse.GetValue(corpusOption)!;
    var manifest = new FileInfo(Path.Combine(corpus.FullName, "manifest.json"));
    if (!manifest.Exists)
    {
        Console.Error.WriteLine($"error: no corpus manifest at {manifest.FullName}");
        return 1;
    }

    if (!TryLoadOptions(parse.GetValue(configOption), out var options))
    {
        return 1;
    }

    IReadOnlyList<BenchCase> cases;
    try
    {
        cases = BenchCase.FromJson(File.ReadAllText(manifest.FullName));
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"error: invalid corpus manifest {manifest.FullName}: {ex.Message}");
        return 1;
    }

    var results = new List<AnalysisResult>(cases.Count);
    foreach (var c in cases)
    {
        var path = Path.Combine(corpus.FullName, "cases", c.File);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"error: corpus case not found: {path}");
            return 1;
        }

        results.Add(Analyzer.Analyze(File.ReadAllText(path), options));
    }

    var report = Bench.Score(cases, results);
    Console.Write(BenchReporter.Render(report));

    // A regression against the labels fails the run; known gaps do not. The separate
    // --fail-on gate for analysing a real draft in CI is a different thing and comes later.
    return report.Regressions.Count == 0 ? 0 : 1;
});

var root = new RootCommand("tare - writing-integrity checks for long-form drafts")
{
    analyze,
    bench,
};

return await root.Parse(args).InvokeAsync();

// Who answers the support question. With no key the model adapter would answer Unknown for
// every claim, which reads in a report exactly like every source checking out, so the absence
// is reported and the run falls back to the verifier that asks nobody. Either way the
// deterministic report is the same one `analyze` prints without the flag.
static IClaimVerifier Verifier()
{
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ClaudeClaimVerifier.KeyVariable)))
    {
        return ClaudeClaimVerifier.Create();
    }

    Console.Error.WriteLine(
        $"note: --verify needs {ClaudeClaimVerifier.KeyVariable} to be set; nothing was verified");
    return NoClaimVerifier.Instance;
}

// An explicit --config wins; otherwise a tare.json beside the working directory is picked up
// automatically; otherwise the calibrated defaults apply. Returns false once it has reported.
static bool TryLoadOptions(FileInfo? explicitConfig, out TareOptions options)
{
    options = TareOptions.Default;
    var path = explicitConfig?.FullName ?? (File.Exists("tare.json") ? "tare.json" : null);
    if (path is null)
    {
        return true;
    }

    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"error: config not found: {path}");
        return false;
    }

    try
    {
        options = TareOptions.FromJson(File.ReadAllText(path));
        return true;
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"error: invalid config {path}: {ex.Message}");
        return false;
    }
}
