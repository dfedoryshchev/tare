using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tare.Core;

/// <summary>
/// Serializes an <see cref="AnalysisResult"/> to a stable JSON schema - the score, band, and
/// the ordered findings - for machine consumers (CI gates, editors, later SARIF). Enum values
/// render as their names and property names are camelCase; this shape is part of the tool's
/// contract, so a field is never renamed or removed without a version note.
/// </summary>
public static class JsonReport
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(AnalysisResult result) =>
        JsonSerializer.Serialize(result, Options);
}
