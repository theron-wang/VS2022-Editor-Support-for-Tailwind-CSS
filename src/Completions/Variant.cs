using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailwindCSSIntellisense.Completions;

internal class Variant
{
    [JsonPropertyName("s")]
    public string Stem { get; set; } = null!;
    [JsonPropertyName("svs")]
    public List<Subvariant>? Subvariants { get; set; }
    [JsonPropertyName("dv")]
    public List<string>? DirectVariants { get; set; }
    [JsonPropertyName("c")]
    public bool? UseColors { get; set; }
    [JsonPropertyName("sp")]
    public bool? UseSpacing { get; set; }
    [JsonPropertyName("o")]
    public bool? UseOpacity { get; set; }
    [JsonPropertyName("n")]
    public bool? HasNegative { get; set; }
}