using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TailwindCSSIntellisense.Completions.V4;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Node;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions;

/// <summary>
/// Provides project configurations (completion data) for each configuration file.
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
public sealed class ProjectConfigurationManager
{
    [Import]
    internal CompletionConfiguration Configuration { get; set; }
    [Import]
    internal DirectoryVersionFinder DirectoryVersionFinder { get; set; }
    [Import]
    internal SettingsProvider SettingsProvider { get; set; }

    internal ImageSource TailwindLogo { get; private set; } = new BitmapImage(new Uri(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "tailwindlogo.png"), UriKind.Relative));
    internal bool Initialized { get; private set; }
    internal bool Initializing { get; private set; }

    internal List<int> Opacity { get; set; }

    /// <summary>
    /// Completion settings for each project, keyed by configuration file paths.
    /// </summary>
    private readonly Dictionary<string, ProjectCompletionValues> _projectCompletionConfiguration = [];
    private ProjectCompletionValues _defaultProjectCompletionConfiguration;

    private readonly Dictionary<TailwindVersion, ProjectCompletionValues> _unsetProjectCompletionConfigurations = [];

    /// <summary>
    /// Initializes the necessary utilities to provide completion
    /// </summary>
    /// <returns>An awaitable <see cref="Task{bool}"/> of type <see cref="bool"/> which returns true if completion should be provided and false if not.</returns>
    public async Task<bool> InitializeAsync()
    {
        if (Initialized || Initializing)
        {
            return true;
        }

        try
        {
            if ((await ShouldInitializeAsync()) == false)
            {
                Initialized = false;
                return false;
            }

            Initializing = true;

            // Initial load of settings, which then triggers class loading based on the versions of the config files
            await SettingsProvider.GetSettingsAsync();

            Initialized = true;

            await VS.StatusBar.ShowMessageAsync("Tailwind CSS IntelliSense initialized");

            return true;
        }
        catch (Exception ex)
        {
            await ex.LogAsync();

            // Clear progress
            await VS.StatusBar.ShowMessageAsync("Tailwind CSS initialization failed: check extension output");

            return false;
        }
        finally
        {
            Initializing = false;
        }
    }

    public ProjectCompletionValues GetUnsetCompletionConfiguration(TailwindVersion version)
    {
        ThreadHelper.JoinableTaskFactory.Run(InitializeAsync);

        if (!_unsetProjectCompletionConfigurations.ContainsKey(version))
        {
            ThreadHelper.JoinableTaskFactory.Run(() => LoadClassesAsync(version));
        }

        return _unsetProjectCompletionConfigurations[version];
    }

    /// <summary>
    /// Returns the ProjectCompletionValues for the given configuration file path.
    /// </summary>
    public ProjectCompletionValues GetCompletionConfigurationByConfigFilePath(string configFile)
    {
        ThreadHelper.JoinableTaskFactory.Run(InitializeAsync);

        return _projectCompletionConfiguration[configFile.ToLower()];
    }

    /// <summary>
    /// For IntelliSense; detect which configuration file this file belongs to and return the completion configuration for it.
    /// </summary>
    public ProjectCompletionValues GetCompletionConfigurationByFilePath(string filePath)
    {
        ThreadHelper.JoinableTaskFactory.Run(InitializeAsync);

        if (filePath is null)
        {
            if (_defaultProjectCompletionConfiguration is not null)
            {
                return _defaultProjectCompletionConfiguration;
            }
            else if (_unsetProjectCompletionConfigurations.Any())
            {
                return _unsetProjectCompletionConfigurations.First().Value;
            }
            else
            {
                // Default to v3
                ThreadHelper.JoinableTaskFactory.Run(LoadClassesV3Async);
                return _unsetProjectCompletionConfigurations[TailwindVersion.V3];
            }
        }

        foreach (var k in _projectCompletionConfiguration.Values)
        {
            if (k.Version >= TailwindVersion.V4)
            {
                if (k.NotApplicablePaths.Any(p => filePath.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)))
                {
                    continue;
                }

                if (k.ApplicablePaths.Any(p => filePath.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return k;
                }
            }
            else
            {
                if (k.ApplicablePaths.Any(p => PathHelpers.PathMatchesGlob(filePath, p)))
                {
                    return k;
                }
            }
        }

        if (_defaultProjectCompletionConfiguration is not null)
        {
            return _defaultProjectCompletionConfiguration;
        }
        else if (_unsetProjectCompletionConfigurations.Any())
        {
            return _unsetProjectCompletionConfigurations.First().Value;
        }
        else
        {
            // Default to v3
            ThreadHelper.JoinableTaskFactory.Run(LoadClassesV3Async);
            return _unsetProjectCompletionConfigurations[TailwindVersion.V3];
        }
    }

    public async Task OnSettingsChangedAsync(TailwindSettings settings)
    {
        _defaultProjectCompletionConfiguration = null;

        // V4 uses BuildFiles as ConfigurationFiles. Since existing code already uses ConfigurationFiles, it's easier to just
        // populate it here rather than rewrite everything to account for BuildFiles.
        foreach (var buildFile in settings.BuildFiles)
        {
            if (settings.ConfigurationFiles.All(cf => !cf.Path.Equals(buildFile.Input, StringComparison.InvariantCultureIgnoreCase)))
            {
                var version = await DirectoryVersionFinder.GetTailwindVersionAsync(buildFile.Input);

                if (version >= TailwindVersion.V4)
                {
                    settings.ConfigurationFiles.Add(new() { Path = buildFile.Input.ToLower() });
                }
            }
        }

        foreach (var file in settings.ConfigurationFiles)
        {
            if (!_projectCompletionConfiguration.TryGetValue(file.Path.ToLower(), out var projectConfig))
            {
                var version = await DirectoryVersionFinder.GetTailwindVersionAsync(file.Path);

                if (!_unsetProjectCompletionConfigurations.TryGetValue(version, out var toCopy))
                {
                    await LoadClassesAsync(version);
                    toCopy = _unsetProjectCompletionConfigurations[version];
                }

                projectConfig = toCopy.Copy();
                _projectCompletionConfiguration.Add(file.Path.ToLower(), projectConfig);

                await CheckForUpdates.UpdateConfigFileFolderAsync(file.Path);
            }

            projectConfig.FilePath = file.Path.ToLower();
        }

        var toRemove = _projectCompletionConfiguration.Keys.Except(settings.ConfigurationFiles.Select(f => f.Path.ToLower())).ToList();

        foreach (var file in toRemove)
        {
            _projectCompletionConfiguration.Remove(file);
        }

        if (settings.ConfigurationFiles.Count > 0)
        {
            _defaultProjectCompletionConfiguration ??= _projectCompletionConfiguration[settings.ConfigurationFiles.First().Path.ToLower()];
        }

        if (Initialized)
        {
            await Configuration.ReloadCustomAttributesAsync(settings);
        }
    }

    private async Task<bool> ShouldInitializeAsync()
    {
        var settings = await SettingsProvider.GetSettingsAsync();
        return settings.ConfigurationFiles.Count > 0 || settings.BuildFiles.Count > 0;
    }

    private async Task LoadClassesV3Async()
    {
        if (_unsetProjectCompletionConfigurations.ContainsKey(TailwindVersion.V3))
        {
            return;
        }

        var project = new ProjectCompletionValues
        {
            Version = TailwindVersion.V3
        };

        var baseFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
        var versionFolder = Path.Combine(baseFolder, TailwindVersion.V3.ToString());
        List<Variant> variants = [];

        var loadTasks = new List<Task>
        {
            LoadJsonAsync<List<Variant>>(Path.Combine(versionFolder, "classes.json"), v => variants = v),
            LoadJsonAsync<List<string>>(Path.Combine(versionFolder, "variants.json"), m => project.Variants = m),
            LoadJsonAsync<Dictionary<string, string>>(Path.Combine(versionFolder, "colors.json"), c => project.ColorMapper = c),
            LoadJsonAsync<List<string>>(Path.Combine(baseFolder, "spacing.json"), spacing =>
            {
                project.SpacingMapper = [];
                foreach (var s in spacing)
                {
                    project.SpacingMapper[s] = s == "px" ? "1px" : $"{float.Parse(s, CultureInfo.InvariantCulture) / 4}rem";
                }
            }),
            LoadJsonAsync<List<int>>(Path.Combine(baseFolder, "opacity.json"), o => Opacity = o),
            LoadJsonAsync<Dictionary<string, List<string>>>(Path.Combine(baseFolder, "tailwindconfig.json"), c => project.ConfigurationValueToClassStems = c),
            LoadJsonAsync<Dictionary<string, string>>(Path.Combine(versionFolder, "descriptions.json"), d => project.DescriptionMapper = d)
        };

        await Task.WhenAll(loadTasks);

        project.Classes = [];

        foreach (var variant in variants)
        {
            var classes = new List<TailwindClass>();

            if (variant.DirectVariants != null && variant.DirectVariants.Count > 0)
            {
                foreach (var v in variant.DirectVariants)
                {
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        classes.Add(new TailwindClass()
                        {
                            Name = variant.Stem
                        });
                    }
                    else
                    {
                        if (v.Contains("{s}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = variant.Stem + "-" + v.Replace("{s}", "{0}"),
                                UseSpacing = true
                            });
                        }
                        else if (v.Contains("{c}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = variant.Stem + "-" + v.Replace("{c}", "{0}"),
                                UseColors = true,
                                UseOpacity = variant.UseOpacity == true
                            });
                        }
                        else
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = variant.Stem + "-" + v
                            });
                        }
                    }
                }
            }

            if (variant.Subvariants != null && variant.Subvariants.Count > 0)
            {
                // Do the same check for each of the subvariants as above

                foreach (var subvariant in variant.Subvariants)
                {
                    if (subvariant.Variants != null)
                    {
                        foreach (var v in subvariant.Variants)
                        {
                            if (string.IsNullOrWhiteSpace(v))
                            {
                                classes.Add(new TailwindClass()
                                {
                                    Name = variant.Stem + "-" + subvariant.Stem
                                });
                            }
                            else
                            {
                                classes.Add(new TailwindClass()
                                {
                                    Name = variant.Stem + "-" + subvariant.Stem + "-" + v
                                });
                            }
                        }
                    }

                    if (subvariant.Stem.Contains("{c}"))
                    {
                        classes.Add(new TailwindClass()
                        {
                            Name = variant.Stem + "-" + subvariant.Stem.Replace("{c}", "{0}"),
                            // Notify the completion provider to show color options
                            UseColors = true,
                            UseOpacity = variant.UseOpacity == true
                        });
                    }
                    else if (subvariant.Stem.Contains("{s}"))
                    {
                        classes.Add(new TailwindClass()
                        {
                            Name = variant.Stem + "-" + subvariant.Stem.Replace("{s}", "{0}"),
                            // Notify the completion provider to show spacing options
                            UseSpacing = true
                        });
                    }
                }
            }

            if ((variant.DirectVariants == null || variant.DirectVariants.Count == 0) && (variant.Subvariants == null || variant.Subvariants.Count == 0))
            {
                var newClass = new TailwindClass()
                {
                    Name = variant.Stem
                };
                if (variant.UseColors == true)
                {
                    newClass.UseColors = true;
                    newClass.UseOpacity = variant.UseOpacity == true;
                    newClass.Name += "-{0}";
                }
                else if (variant.UseSpacing == true)
                {
                    newClass.UseSpacing = true;
                    newClass.Name += "-{0}";
                }
                classes.Add(newClass);
            }

            project.Classes.AddRange(classes);

            if (variant.HasNegative == true)
            {
                var negativeClasses = classes.Select(c =>
                {
                    return new TailwindClass()
                    {
                        Name = $"-{c.Name}",
                        UseColors = c.UseColors,
                        UseSpacing = c.UseSpacing
                    };
                }).ToList();

                project.Classes.AddRange(negativeClasses);
            }
        }
        foreach (var stems in project.ConfigurationValueToClassStems.Values)
        {
            foreach (var stem in stems)
            {
                string name;
                if (stem.Contains('{'))
                {
                    var replace = stem.Substring(stem.IndexOf('{'), stem.IndexOf('}') - stem.IndexOf('{') + 1);
                    name = stem.Replace(replace, "");
                }
                else
                {
                    name = stem.EndsWith("-") ? stem : stem + "-";
                }

                if (stem.Contains(":"))
                {
                    project.Variants.Add($"{name.Replace(":-", "")}-[]");
                }
                else
                {
                    if (project.Classes.All(c => (c.Name == name && c.HasArbitrary == false) || c.Name != name))
                    {
                        project.Classes.Add(new TailwindClass()
                        {
                            Name = name,
                            HasArbitrary = true
                        });
                    }
                }
            }
        }

        _unsetProjectCompletionConfigurations[TailwindVersion.V3] = project;
    }

    private async Task LoadClassesAsync(TailwindVersion version)
    {
        if (_unsetProjectCompletionConfigurations.ContainsKey(version))
        {
            return;
        }

        if (version == TailwindVersion.V3)
        {
            await LoadClassesV3Async();
            return;
        }

        var project = new ProjectCompletionValues
        {
            Version = version
        };

        var baseFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
        var versionFolder = Path.Combine(baseFolder, version.ToString());

        List<ClassType> classTypes = [];

        var loadTasks = new List<Task>
        {
            LoadJsonAsync<List<ClassType>>(Path.Combine(versionFolder, "classes.json"), v => classTypes = v),
            LoadJsonAsync<Dictionary<string, string>>(Path.Combine(versionFolder, "colors.json"), c => project.ColorMapper = c),
            LoadJsonAsync<List<string>>(Path.Combine(baseFolder, "spacing.json"), spacing =>
            {
                project.SpacingMapper = [];
                foreach (var s in spacing)
                {
                    project.SpacingMapper[s] = s == "px" ? "1px" : $"{float.Parse(s, CultureInfo.InvariantCulture) / 4}rem";
                }
            }),
            LoadJsonAsync<List<int>>(Path.Combine(baseFolder, "opacity.json"), o => Opacity = o),
            LoadJsonAsync<Dictionary<string, List<string>>>(Path.Combine(baseFolder, "tailwindconfig.json"), c => project.ConfigurationValueToClassStems = c),
            LoadJsonAsync<Dictionary<string, string>>(Path.Combine(versionFolder, "descriptions.json"), d => project.DescriptionMapper = d),
            LoadJsonAsync<Dictionary<string, string>>(Path.Combine(versionFolder, "theme.json"), d => project.CssVariables = d),
            LoadJsonAsync<Dictionary<string, string>>(Path.Combine(versionFolder, "variants.json"), d => project.VariantsToDescriptions = d)
        };

        await Task.WhenAll(loadTasks);

        project.Variants = [.. project.VariantsToDescriptions.Keys];

        project.Classes = [];

        foreach (var classType in classTypes)
        {
            var classes = new List<TailwindClass>();

            if (classType.DirectVariants != null && classType.DirectVariants.Count > 0)
            {
                foreach (var v in classType.DirectVariants)
                {
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        classes.Add(new TailwindClass()
                        {
                            Name = classType.Stem
                        });
                    }
                    else
                    {
                        if (v.Contains("{s}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = classType.Stem + "-" + v.Replace("{s}", "{0}"),
                                UseSpacing = true
                            });
                        }
                        else if (v.Contains("{c}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = classType.Stem + "-" + v.Replace("{c}", "{0}"),
                                UseColors = true,
                                UseOpacity = true
                            });
                        }
                        else if (v.Contains("{n}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = classType.Stem + "-" + v.Replace("{n}", "{0}"),
                                UseNumbers = true
                            });
                        }
                        else if (v.Contains("{%}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = classType.Stem + "-" + v.Replace("{%}", "{0}"),
                                UsePercent = true
                            });
                        }
                        else if (v.Contains("{f}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = classType.Stem + "-" + v.Replace("{f}", "{0}"),
                                UseFractions = true
                            });
                        }
                        else
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = classType.Stem + "-" + v
                            });
                        }
                    }
                }
            }

            if (classType.Subvariants != null && classType.Subvariants.Count > 0)
            {
                // Do the same check for each of the subvariants as above

                foreach (var subvariant in classType.Subvariants)
                {
                    if (subvariant.Variants != null)
                    {
                        foreach (var v in subvariant.Variants)
                        {
                            if (string.IsNullOrWhiteSpace(v))
                            {
                                classes.Add(new TailwindClass()
                                {
                                    Name = classType.Stem + "-" + subvariant.Stem
                                });
                            }
                            else
                            {
                                classes.Add(new TailwindClass()
                                {
                                    Name = classType.Stem + "-" + subvariant.Stem + "-" + v
                                });
                            }
                        }
                    }

                    if (subvariant.HasArbitrary == true)
                    {
                        classes.Add(new TailwindClass()
                        {
                            Name = classType.Stem + "-" + subvariant.Stem + "-",
                            HasArbitrary = true
                        });
                    }
                }
            }

            if ((classType.DirectVariants == null || classType.DirectVariants.Count == 0) && (classType.Subvariants == null || classType.Subvariants.Count == 0))
            {
                var newClass = new TailwindClass()
                {
                    Name = classType.Stem
                };
                if (classType.UseColors == true)
                {
                    newClass.UseColors = true;
                    newClass.UseOpacity = true;
                    newClass.Name = newClass.Name.Replace("{c}", "{0}");
                }
                else if (classType.UseSpacing == true)
                {
                    newClass.UseSpacing = true;
                    newClass.Name = newClass.Name.Replace("{s}", "{0}");
                }
                else if (classType.UseNumbers == true)
                {
                    newClass.UseNumbers = true;
                    newClass.Name = newClass.Name.Replace("{n}", "{0}");
                }
                else if (classType.UsePercent == true)
                {
                    newClass.UsePercent = true;
                    newClass.Name = newClass.Name.Replace("{%}", "{0}");
                }
                else if (classType.UseFractions == true)
                {
                    newClass.UseFractions = true;
                    newClass.Name = newClass.Name.Replace("{f}", "{0}");
                }
                classes.Add(newClass);
            }

            if (classType.HasArbitrary == true || classType.UseFractions == true || classType.UseSpacing == true || classType.UsePercent == true ||
                classType.UseColors == true || classType.UseNumbers == true)
            {
                classes.Add(new TailwindClass()
                {
                    Name = classType.Stem.Replace("{c}", "")
                        .Replace("{s}", "")
                        .Replace("{n}", "")
                        .Replace("{%}", "")
                        .Replace("{f}", "").TrimEnd('-') + "-",
                    HasArbitrary = true
                });
            }

            project.Classes.AddRange(classes);

            if (classType.HasNegative == true)
            {
                var negativeClasses = classes.Select(c =>
                {
                    return new TailwindClass()
                    {
                        Name = $"-{c.Name}",
                        UseColors = c.UseColors,
                        UseSpacing = c.UseSpacing,
                        UseNumbers = c.UseNumbers,
                        UsePercent = c.UsePercent,
                        UseFractions = c.UseFractions,
                        UseOpacity = c.UseOpacity,
                        HasArbitrary = c.HasArbitrary
                    };
                }).ToList();

                project.Classes.AddRange(negativeClasses);
            }
        }

        _unsetProjectCompletionConfigurations[version] = project;
    }

    private async Task LoadJsonAsync<T>(string path, Action<T> process)
    {
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var data = await JsonSerializer.DeserializeAsync<T>(fs);
        process(data);
    }
}
