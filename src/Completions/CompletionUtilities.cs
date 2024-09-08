using Community.VisualStudio.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.Completions
{
    /// <summary>
    /// Provides basic utilities for use in <see cref="CssCompletionSource"/> and <see cref="HtmlCompletionSource"/>
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class CompletionUtilities
    {
        [Import]
        internal ConfigFileScanner Scanner { get; set; }
        [Import]
        internal CompletionConfiguration Configuration { get; set; }

        internal ImageSource TailwindLogo { get; private set; } = new BitmapImage(new Uri(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "tailwindlogo.png"), UriKind.Relative));
        internal bool Initialized { get; private set; }
        internal List<TailwindClass> Classes { get; set; }
        internal List<string> Modifiers { get; set; }
        internal List<string> Screen { get; set; } = new List<string>() { "sm", "md", "lg", "xl", "2xl" };
        internal List<int> Opacity { get; set; }

        internal string Prefix { get; set; }

        internal Dictionary<string, string> ColorToRgbMapper { get; set; } = new Dictionary<string, string>();
        internal Dictionary<string, string> SpacingMapper { get; set; } = new Dictionary<string, string>();
        internal Dictionary<string, ImageSource> ColorToRgbMapperCache { get; private set; } = new Dictionary<string, ImageSource>();
        internal Dictionary<string, List<string>> ConfigurationValueToClassStems { get; private set; } = new Dictionary<string, List<string>>();

        internal Dictionary<string, Dictionary<string, string>> CustomColorMappers { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        internal Dictionary<string, Dictionary<string, string>> CustomSpacingMappers { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        internal Dictionary<string, string> DescriptionMapper { get; set; } = new Dictionary<string, string>();
        internal Dictionary<string, string> CustomDescriptionMapper { get; set; } = new Dictionary<string, string>();

        internal List<string> PluginClasses { get; set; }
        internal List<string> PluginModifiers { get; set; }

        /// <summary>
        /// Initializes the necessary utilities to provide completion
        /// </summary>
        /// <returns>An awaitable <see cref="Task{bool}"/> of type <see cref="bool"/> which returns true if completion should be provided and false if not.</returns>
        public async Task<bool> InitializeAsync()
        {
            if (Initialized)
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

                await VS.StatusBar.ShowMessageAsync("Loading Tailwind CSS classes");
                await LoadClassesAsync();
                await VS.StatusBar.ShowMessageAsync("Loading Tailwind CSS configuration");

                await Configuration.InitializeAsync(this);
                await VS.StatusBar.ShowMessageAsync("Tailwind CSS IntelliSense initialized");

                Initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                await ex.LogAsync();

                // Clear progress
                await VS.StatusBar.ShowMessageAsync("Tailwind CSS initialization failed: check extension output");

                return false;
            }
        }

        private async Task<bool> ShouldInitializeAsync()
        {
            return (await Scanner.FindConfigurationFilePathAsync()) != null;
        }

        private async Task LoadClassesAsync()
        {
            var baseFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
            List<Variant> variants = [];

            var loadTasks = new List<Task>
            {
                LoadJsonAsync<List<Variant>>(Path.Combine(baseFolder, "tailwindclasses.json"), v => variants = v),
                LoadJsonAsync<List<string>>(Path.Combine(baseFolder, "tailwindmodifiers.json"), m => Modifiers = m),
                LoadJsonAsync<Dictionary<string, string>>(Path.Combine(baseFolder, "tailwindrgbmapper.json"), c => ColorToRgbMapper = c),
                LoadJsonAsync<List<string>>(Path.Combine(baseFolder, "tailwindspacing.json"), s => ProcessSpacing(s)),
                LoadJsonAsync<List<int>>(Path.Combine(baseFolder, "tailwindopacity.json"), o => Opacity = o),
                LoadJsonAsync<Dictionary<string, List<string>>>(Path.Combine(baseFolder, "tailwindconfig.json"), c => ConfigurationValueToClassStems = c),
                LoadJsonAsync<Dictionary<string, string>>(Path.Combine(baseFolder, "tailwinddesc.json"), d => DescriptionMapper = d)
            };

            await Task.WhenAll(loadTasks);

            Classes = new List<TailwindClass>();

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
                                // Notify the completion provider to show color options
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

                Classes.AddRange(classes);

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

                    Classes.AddRange(negativeClasses);
                }
            }
            foreach (var stems in ConfigurationValueToClassStems.Values)
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
                        Modifiers.Add($"{name.Replace(":-", "")}-[]");
                    }
                    else
                    {
                        if (Classes.All(c => (c.Name == name && c.SupportsBrackets == false) || c.Name != name))
                        {
                            Classes.Add(new TailwindClass()
                            {
                                Name = name,
                                SupportsBrackets = true
                            });
                        }
                    }
                }
            }
        }

        private void ProcessSpacing(List<string> spacing)
        {
            SpacingMapper = new Dictionary<string, string>();
            foreach (var s in spacing)
            {
                SpacingMapper[s] = s == "px" ? "1px" : $"{float.Parse(s, CultureInfo.InvariantCulture) / 4}rem";
            }
        }

        internal ImageSource GetImageFromColor(string stem, string color, int opacity = 100)
        {
            if (ColorToRgbMapperCache.TryGetValue($"{stem}/{color}/{opacity}", out var result) || ColorToRgbMapperCache.TryGetValue($"{color}/{opacity}", out result))
            {
                return result;
            }

            string value;
            if (CustomColorMappers != null)
            {
                if (CustomColorMappers.TryGetValue(stem, out var dict) == false || (dict.TryGetValue(color, out value) && ColorToRgbMapper.TryGetValue(color, out var value2) && value == value2))
                {
                    if (ColorToRgbMapperCache.TryGetValue($"{color}/{opacity}", out result))
                    {
                        return result;
                    }

                    if (ColorToRgbMapper.TryGetValue(color, out value) == false)
                    {
                        return TailwindLogo;
                    }
                }
            }
            else if (ColorToRgbMapper.TryGetValue(color, out value) == false)
            {
                return TailwindLogo;
            }

            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("{noparse}"))
            {
                return TailwindLogo;
            }

            var rgb = value.Split(',');

            if (rgb.Length == 0)
            {
                // Something wrong happened: fall back to default tailwind icon
                return TailwindLogo;
            }
            var r = byte.Parse(rgb[0]);
            var g = byte.Parse(rgb[1]);
            var b = byte.Parse(rgb[2]);
            var a = (byte)Math.Round(opacity / 100d * 255);

            var pen = new Pen() { Thickness = 8, Brush = new SolidColorBrush(Color.FromArgb(a, r, g, b)) };
            var mainImage = new GeometryDrawing() { Geometry = new RectangleGeometry(new Rect(4, 5, 9, 8)), Pen = pen };

            // https://stackoverflow.com/questions/37663993/preventing-icon-color-and-size-distortions-when-bundling-a-visual-studio-project
            var pen2 = new Pen() { Thickness = 1, Brush = new SolidColorBrush(Color.FromArgb(1, 0, 255, 255)) };
            var vsPrevent = new GeometryDrawing() { Geometry = new RectangleGeometry(new Rect(16, 0, 1, 1)), Pen = pen2 };

            var geometry = new DrawingGroup();
            geometry.Children.Add(mainImage);
            geometry.Children.Add(vsPrevent);

            result = new DrawingImage
            {
                Drawing = geometry
            };

            var key = $"{color}/{opacity}";
            if (string.IsNullOrEmpty(stem) == false)
            {
                key = $"{stem}/{key}";
            }

            ColorToRgbMapperCache[key] = result;

            return result;
        }

        private async Task LoadJsonAsync<T>(string path, Action<T> process)
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var data = await JsonSerializer.DeserializeAsync<T>(fs);
            process(data);
        }
    }
}
