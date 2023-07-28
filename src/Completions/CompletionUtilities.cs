using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Utilities;
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
        internal List<TailwindClass> Classes { get; private set; }
        internal List<string> Modifiers { get; set; }
        internal List<string> Spacing { get; set; }
        internal List<int> Opacity { get; set; }
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
                    Initialized = false;
                    return false;
                }

                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);

                await VS.StatusBar.ShowProgressAsync("Loading TailwindCSS classes", 1, 3);
                await LoadClassesAsync();
                await VS.StatusBar.ShowProgressAsync("Loading TailwindCSS configuration", 2, 3);

                await Configuration.InitializeAsync(this);
                await VS.StatusBar.ShowProgressAsync("TailwindCSS Intellisense initialized", 3, 3);

                Initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                await ex.LogAsync();

                // Clear progress
                await VS.StatusBar.ShowProgressAsync("", 4, 4);
                await VS.StatusBar.ShowMessageAsync("TailwindCSS initialization failed: check extension output");

                return false;
            }
            finally
            {
                await VS.StatusBar.ShowProgressAsync("", 4, 4);
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
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
            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindopacity.json"), FileMode.Open, FileAccess.Read))
            {
                Opacity = await JsonSerializer.DeserializeAsync<List<int>>(fs);
            }

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
                            SupportsBrackets = c.SupportsBrackets,
                            UseColors = c.UseColors,
                            UseSpacing = c.UseSpacing
                        };
                    }).ToList();

                    Classes.AddRange(negativeClasses);
                }
            }
        }

        /// <summary>
        /// Gets the corresponding icon for a certain color class
        /// </summary>
        /// <param name="color">The color to generate an icon for (i.e. amber-100, blue-500)</param>
        /// <param name="opacity">The opacity of the color (0-100)</param>
        /// <returns>An <see cref="ImageSource"/> which contains a square displaying the requested color</returns>
        internal ImageSource GetImageFromColor(string color, int opacity = 100)
        {
            if (ColorToRgbMapperCache.TryGetValue($"{color}/{opacity}", out var result))
            {
                return result;
            }

            if (ColorToRgbMapper.TryGetValue(color, out string value) == false || string.IsNullOrWhiteSpace(value))
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

            var pen = new Pen() { Thickness = 7, Brush = new SolidColorBrush(Color.FromArgb(a, r, g, b)) };
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

            ColorToRgbMapperCache.Add($"{color}/{opacity}", result);

            return result;
        }

        /// <summary>
        /// Gets the hex code for a certain color
        /// </summary>
        /// <param name="color">The color to generate an description for (i.e. amber-100, blue-500)</param>
        /// <param name="opacity">The opacity of the color (0-100)</param>
        /// <returns>A 6-digit hex code representing the color</returns>
        internal string GetColorDescription(string color, int opacity = 100)
        {
            // Invalid color or value is empty when color is current, inherit, or transparent
            if (ColorToRgbMapper.TryGetValue(color, out string value) == false || string.IsNullOrWhiteSpace(value))
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
            var a = (byte)Math.Round(opacity / 100d * 255);

            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }
    }
}
