using System;
using System.Collections.Generic;

namespace TailwindCSSIntellisense.Settings;

internal class TailwindSettingsProjectOnly
{
    public string ConfigurationFile { get; set; }
    [Obsolete]
    /// <summary>
    /// Maintained for backwards compatibility: use <see cref="BuildFiles"/> instead.
    /// </summary>
    public string InputCssFile { get; set; }
    [Obsolete]
    /// <summary>
    /// Maintained for backwards compatibility: use <see cref="BuildFiles"/> instead.
    /// </summary>
    public string OutputCssFile { get; set; }
    public List<BuildPair> BuildFiles { get; set; } = [];
    public string PackageConfigurationFile { get; set; }
    public bool UseCli { get; set; }
}