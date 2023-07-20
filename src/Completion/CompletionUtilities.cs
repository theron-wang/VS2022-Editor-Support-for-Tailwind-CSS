using Community.VisualStudio.Toolkit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
        internal Dictionary<string, List<TailwindClass>> StemToClassesMatch { get; private set; }
        internal List<string> Modifiers { get; set; }
        internal List<string> Spacing { get; set; }
        internal Dictionary<string, string> ColorToRgbMapper { get; set; }
        internal Dictionary<string, ImageSource> ColorToRgbMapperCache { get; private set; } = new Dictionary<string, ImageSource>();

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
                    if (Initialized)
                    {
                        await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
                        await VS.StatusBar.ShowProgressAsync("No TailwindCSS configuration file found: Intellisense disabled", 4, 4);
                    }
                    Initialized = false;
                    return false;
                }

                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                await VS.StatusBar.ShowProgressAsync("Searching for TailwindCSS configuration file", 1, 4);

                await VS.StatusBar.ShowProgressAsync("Loading TailwindCSS classes", 2, 4);
                await LoadClassesAsync();
                await VS.StatusBar.ShowProgressAsync("Loading TailwindCSS configuration", 3, 4);

                await Configuration.InitializeAsync(this);
                await VS.StatusBar.ShowProgressAsync("TailwindCSS Intellisense initialized", 4, 4);

                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);

                Initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);

                await ex.LogAsync();

                // Clear progress
                await VS.StatusBar.ShowProgressAsync("", 4, 4);
                await VS.StatusBar.ShowMessageAsync("TailwindCSS initialization failed: check extension output");

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
            List<Variant> variants;

            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindclasses.json"), FileMode.Open, FileAccess.Read))
            {
                variants = await JsonSerializer.DeserializeAsync<List<Variant>>(fs);
            }
            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindmodifiers.json"), FileMode.Open, FileAccess.Read))
            {
                Modifiers = await JsonSerializer.DeserializeAsync<List<string>>(fs);
            }
            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindrgbmapper.json"), FileMode.Open, FileAccess.Read))
            {
                ColorToRgbMapper = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(fs);
            }
            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindspacing.json"), FileMode.Open, FileAccess.Read))
            {
                Spacing = await JsonSerializer.DeserializeAsync<List<string>>(fs);
            }

            StemToClassesMatch = new Dictionary<string, List<TailwindClass>>();

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
                            else
                            {
                                classes.Add(new TailwindClass()
                                {
                                    Name = variant.Stem + "-" + v
                                });
                            }
                        }
                    }

                    // Generally speaking, tailwind classes with a number as a variant support arbitrary values;
                    // however, there are still some exceptions but those classes are not as commonly used.
                    if (variant.DirectVariants.Any(x => int.TryParse(x, out _)) && variant.UseColors != true && variant.UseSpacing != true)
                    {
                        classes.Add(new TailwindClass()
                        {
                            Name = variant.Stem,
                            SupportsBrackets = true
                        });
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

                            // Generally speaking, tailwind classes with a number as a variant support arbitrary values;
                            // however, there are still some exceptions but those classes are not as commonly used.
                            if (subvariant.Variants.Any(x => int.TryParse(x, out _)))
                            {
                                classes.Add(new TailwindClass()
                                {
                                    Name = variant.Stem + "-" + subvariant.Stem,
                                    SupportsBrackets = true
                                });
                            }
                        }

                        if (subvariant.Stem.Contains("{c}"))
                        {
                            classes.Add(new TailwindClass()
                            {
                                Name = variant.Stem + "-" + subvariant.Stem.Replace("{c}", "{0}"),
                                // Notify the completion provider to show color options
                                UseColors = true
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

                if (variant.UseColors == true)
                {
                    classes.Add(new TailwindClass()
                    {
                        // When displaying to user, string.Format is used to insert colors
                        Name = variant.Stem + "-{0}",
                        UseColors = true
                    });
                }
                if (variant.UseSpacing == true)
                {
                    classes.Add(new TailwindClass()
                    {
                        // When displaying to user, string.Format is used to insert spacing
                        Name = variant.Stem + "-{0}",
                        UseSpacing = true
                    });
                }
                if (variant.UseSpacing == true || variant.UseColors == true)
                {
                    classes.Add(new TailwindClass()
                    {
                        Name = variant.Stem,
                        SupportsBrackets = true
                    });
                }


                StemToClassesMatch.Add(variant.Stem, classes);
            }
        }

        /// <summary>
        /// Gets the corresponding icon for a certain color class
        /// </summary>
        /// <param name="color">The color to generate an icon for (i.e. amber-100, blue-500)</param>
        /// <returns>An <see cref="ImageSource"/> which contains a square displaying the requested color</returns>
        internal ImageSource GetImageFromColor(string color)
        {
            if (ColorToRgbMapperCache.TryGetValue(color, out var result))
            {
                return result;
            }

            if (ColorToRgbMapper.TryGetValue(color, out string value) == false || value == null)
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

            var pen = new Pen() { Thickness = 7, Brush = new SolidColorBrush(Color.FromArgb(255, r, g, b)) };
            var mainImage = new GeometryDrawing() { Geometry = new RectangleGeometry(new Rect(11, 2, 5, 8)), Pen = pen };

            // https://stackoverflow.com/questions/37663993/preventing-icon-color-and-size-distortions-when-bundling-a-visual-studio-project
            var pen2 = new Pen() { Thickness = 1, Brush = new SolidColorBrush(Color.FromArgb(1, 0, 255, 255)) };
            var vsPrevent = new GeometryDrawing() { Geometry = new RectangleGeometry(new Rect(18, -2, 1, 1)), Pen = pen2 };

            var geometry = new DrawingGroup();
            geometry.Children.Add(mainImage);
            geometry.Children.Add(vsPrevent);
            
            result = new DrawingImage
            {
                Drawing = geometry
            };

            ColorToRgbMapperCache.Add(color, result);

            return result;
        }

        /// <summary>
        /// Gets the hex code for a certain color
        /// </summary>
        /// <param name="color">The color to generate an description for (i.e. amber-100, blue-500)</param>
        /// <returns>A 6-digit hex code representing the color</returns>
        internal string GetColorDescription(string color)
        {
            // Invalid color or value is null when color is current, inherit, or transparent
            if (ColorToRgbMapper.TryGetValue(color, out string value) == false || value == null)
            {
                return null;
            }
            
            var rgb = value.Split(',');

            if (rgb.Length == 0)
            {
                // Something wrong happened: return null which the caller can interpret
                return null;
            }
            var r = byte.Parse(rgb[0]);
            var g = byte.Parse(rgb[1]);
            var b = byte.Parse(rgb[2]);

            return $"#{r:x2}{g:x2}{b:x2}";
        }
    }
}
