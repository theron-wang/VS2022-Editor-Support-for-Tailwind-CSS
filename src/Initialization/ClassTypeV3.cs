using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailwindCSSIntellisense.Initialization;

internal class ClassTypeV3 : ClassTypeBase
{
    [JsonPropertyName("svs")]
    public List<ClassSubTypeV3>? Subvariants { get; set; }

    [JsonPropertyName("o")]
    public bool? UseOpacity { get; set; }
}
