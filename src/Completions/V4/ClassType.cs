using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailwindCSSIntellisense.Completions.V4;
internal class ClassType
{
    [JsonPropertyName("s")]
    public string Stem { get; set; }

    [JsonPropertyName("sv")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ClassSubType> Subvariants { get; set; }

    [JsonPropertyName("dv")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> DirectVariants { get; set; }

    [JsonPropertyName("c")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseColors { get; set; }

    [JsonPropertyName("sp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseSpacing { get; set; }

    [JsonPropertyName("p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UsePercent { get; set; }

    [JsonPropertyName("f")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseFractions { get; set; }

    [JsonPropertyName("d")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseNumbers { get; set; }

    [JsonPropertyName("n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
