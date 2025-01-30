using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TailwindCSSIntellisense.Settings;

internal class TailwindSettingsProjectOnly
{
    private const string Schema = "https://raw.githubusercontent.com/theron-wang/VS2022-Editor-Support-for-Tailwind-CSS/refs/heads/main/tailwind.extension.schema.json";

    [JsonPropertyName("$schema")]
    public string SchemaProperty => Schema;

    [Obsolete]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    /// <summary>
    /// Maintained for backwards compatibility: use <see cref="ConfigurationFiles"/> instead.
    /// </summary>
    public string ConfigurationFile { get; set; }
    [Obsolete]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    /// <summary>
    /// Maintained for backwards compatibility: use <see cref="BuildFiles"/> instead.
    /// </summary>
    public string InputCssFile { get; set; }
    [Obsolete]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    /// <summary>
    /// Maintained for backwards compatibility: use <see cref="BuildFiles"/> instead.
    /// </summary>
    public string OutputCssFile { get; set; }
    public List<ConfigurationFile> ConfigurationFiles { get; set; } = [];
    public List<BuildPair> BuildFiles { get; set; } = [];
    public string PackageConfigurationFile { get; set; } = "";
    public CustomRegexes CustomRegexes { get; set; } = new();
    public bool UseCli { get; set; }
}