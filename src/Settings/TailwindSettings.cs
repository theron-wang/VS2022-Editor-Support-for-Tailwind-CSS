﻿using System.Collections.Generic;
using TailwindCSSIntellisense.Options;

namespace TailwindCSSIntellisense.Settings;

public class TailwindSettings
{
    public List<ConfigurationFile> ConfigurationFiles { get; set; } = [];
    public string DefaultOutputCssName { get; set; } = "";
    public string[] OnSaveTriggerFileExtensions { get; set; } = [];
    public List<BuildPair> BuildFiles { get; set; } = [];
    public string? PackageConfigurationFile { get; set; }
    public bool UseCli { get; set; }
    public string? TailwindCliPath { get; set; }
    public bool EnableTailwindCss { get; set; }
    public BuildProcessOptions BuildType { get; set; }
    public SortClassesOptions SortClassesType { get; set; }
    public string? BuildScript { get; set; }
    public bool OverrideBuild { get; set; }
    public bool AutomaticallyMinify { get; set; }
    public CustomRegexes CustomRegexes { get; set; } = new();
}
