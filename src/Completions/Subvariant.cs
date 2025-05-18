﻿using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailwindCSSIntellisense.Completions;

internal class Subvariant
{
    [JsonPropertyName("ss")]
    public string Stem { get; set; } = null!;
    [JsonPropertyName("v")]
    public List<string> Variants { get; set; } = null!;
}