# Usage

Call the analyzer with a document and read the band off the result.

```csharp
var result = Analyzer.Analyze(File.ReadAllText(path));
Console.WriteLine(result.Band);
```

The score is deterministic, so the same input gives the same number every run.

```bash
tare analyze draft.md --json
```
