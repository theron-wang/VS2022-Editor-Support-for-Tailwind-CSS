using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailwindCSSIntellisense.Completions.V4;
internal class ClassType
{
    [JsonPropertyName("s")]
    public string Stem { get; set; } = null!;

    [JsonPropertyName("sv")]
    public List<ClassSubType>? Subvariants { get; set; }

    [JsonPropertyName("dv")]
    public List<string>? DirectVariants { get; set; }

    [JsonPropertyName("c")]
    public bool? UseColors { get; set; }

    [JsonPropertyName("sp")]
    public bool? UseSpacing { get; set; }

    [JsonPropertyName("p")]
    public bool? UsePercent { get; set; }

    [JsonPropertyName("f")]
    public bool? UseFractions { get; set; }

    [JsonPropertyName("d")]
    public bool? UseNumbers { get; set; }

    [JsonPropertyName("n")]
    public bool? HasNegative { get; set; }

    /// <summary>
    /// The absence of this property does not necessarily mean that arbitrary values are not supported.
    /// Colors, spacing, percentages, fractions, numbers all support this already, but theirs will be 
    /// set to null. This property is only for the ones that don't fall into those categories.
    /// </summary>
    [JsonPropertyName("a")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasArbitrary { get; set; }
}
