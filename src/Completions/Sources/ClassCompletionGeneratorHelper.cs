using EnvDTE;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Completions.Sources
{
    internal class ClassCompletionGeneratorHelper
    {
        /// <summary>
        /// Gets relevant Tailwind CSS completions for a certain input
        /// </summary>
        /// <param name="classRaw">The current class text from the caret to the previous break area (i.e. ' ')</param>
        /// <param name="completionUtils">The <see cref="CompletionUtilities"/> class</param>
        /// <returns>A list of completions</returns>
        internal static List<Completion> GetCompletions(string classRaw, CompletionUtilities completionUtils)
        {
            var modifiers = classRaw.Split(':').ToList();

            var currentClass = modifiers.Last();
            var prefix = completionUtils.Prefix;

            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = null;
            }

            modifiers.RemoveAt(modifiers.Count - 1);

            var completions = new List<Completion>();

            var modifiersAsString = string.Join(":", modifiers);
            if (string.IsNullOrWhiteSpace(modifiersAsString) == false)
            {
                modifiersAsString += ":";
            }
            var segments = currentClass.Split('-');

            if (string.IsNullOrWhiteSpace(currentClass) == false)
            {
                var currentClassStem = currentClass.Split('-')[0];
                var searchClass = $"-{currentClass.TrimStart('-')}";
                var startsWith = currentClass[0].ToString();
                if (currentClass.Length > 1)
                {
                    startsWith += currentClass[1];
                }
                var scope = completionUtils.Classes.Where(
                    c => c.Name.Contains(searchClass) || (prefix + c.Name).StartsWith(startsWith) || c.UseColors);

                if (currentClass.StartsWith("-") == false)
                {
                    scope = scope.OrderBy(c => c.Name.StartsWith("-"));
                }

                foreach (var twClass in scope)
                {
                    if (twClass.UseColors)
                    {
                        IEnumerable<string> colors;

                        if (completionUtils.CustomColorMappers != null && completionUtils.CustomColorMappers.ContainsKey(twClass.Name))
                        {
                            colors = completionUtils.CustomColorMappers[twClass.Name].Keys;
                        }
                        else
                        {
                            colors = completionUtils.ColorToRgbMapper.Keys;
                        }

                        if (twClass.Name.StartsWith(currentClassStem) == false)
                        {
                            // If you're typing in 'tra' to get transform, you don't need a bunch of
                            // completions saying bg-neutral
                            colors = colors.Where(c => c.StartsWith(currentClassStem));
                        }

                        foreach (var color in colors)
                        {
                            var className = string.Format(twClass.Name, color);

                            if (className.StartsWith("-"))
                            {
                                className = $"-{prefix}{className.TrimStart('-')}";
                            }
                            else
                            {
                                className = $"{prefix}{className}";
                            }

                            completions.Add(
                                        new Completion(className,
                                                            modifiersAsString + className,
                                                            completionUtils.GetDescription(twClass.Name, color, opacity: false),
                                                            completionUtils.GetImageFromColor(twClass.Name, color, color == "transparent" ? 0 : 100),
                                                            null));

                            if (twClass.UseOpacity && currentClass.Contains(color) && currentClass.Contains('/'))
                            {
                                var description = completionUtils.GetDescription(twClass.Name, color, opacity: true);

                                foreach (var opacity in completionUtils.Opacity)
                                {
                                    completions.Add(
                                            new Completion($"{className}/{opacity}",
                                                                $"{modifiersAsString}{className}/{opacity}",
                                                                string.Format(description, opacity / 100f),
                                                                completionUtils.GetImageFromColor(twClass.Name, color, opacity),
                                                                null));
                                }
                                completions.Add(
                                            new Completion(className + "/[...]",
                                                                modifiersAsString + className + "/[]",
                                                                className + "/[...]",
                                                                completionUtils.TailwindLogo,
                                                                null));
                            }


                        }
                    }
                    else if (twClass.UseSpacing)
                    {
                        IEnumerable<string> spacings;

                        if (completionUtils.CustomSpacingMappers != null && completionUtils.CustomSpacingMappers.TryGetValue(twClass.Name, out var value))
                        {
                            spacings = value.Keys;
                        }
                        else
                        {
                            spacings = completionUtils.SpacingMapper.Keys;
                        }

                        foreach (var spacing in spacings)
                        {
                            var className = string.IsNullOrWhiteSpace(spacing) ? twClass.Name.Replace("-{0}", "") : string.Format(twClass.Name, spacing);

                            if (className.StartsWith("-"))
                            {
                                className = $"-{prefix}{className.TrimStart('-')}";
                            }
                            else
                            {
                                className = $"{prefix}{className}";
                            }

                            completions.Add(
                                new Completion(className,
                                                    modifiersAsString + className,
                                                    completionUtils.GetDescription(twClass.Name, spacing),
                                                    completionUtils.TailwindLogo,
                                                    null));
                        }
                    }
                    else if (twClass.SupportsBrackets)
                    {
                        var className = twClass.Name;

                        if (className.StartsWith("-"))
                        {
                            className = $"-{prefix}{className.TrimStart('-')}";
                        }
                        else
                        {
                            className = $"{prefix}{className}";
                        }

                        completions.Add(
                        new Completion(className + "[...]",
                                            modifiersAsString + prefix + twClass.Name + "[]",
                                            twClass.Name + "[...]",
                                            completionUtils.TailwindLogo,
                                            null));
                    }
                    else
                    {
                        var className = twClass.Name;

                        if (className.StartsWith("-"))
                        {
                            className = $"-{prefix}{className.TrimStart('-')}";
                        }
                        else
                        {
                            className = $"{prefix}{className}";
                        }

                        completions.Add(
                        new Completion(className,
                                            modifiersAsString + prefix + twClass.Name,
                                            completionUtils.GetDescription(twClass.Name),
                                            completionUtils.TailwindLogo,
                                            null));
                    }
                }

                if (completionUtils.PluginClasses != null)
                {
                    foreach (var pluginClass in completionUtils.PluginClasses)
                    {
                        if (pluginClass.EndsWith("[]"))
                        {
                            completions.Add(
                                new Completion3(pluginClass.Replace("[]", "[...]"),
                                                    modifiersAsString + prefix + pluginClass + "[]",
                                                    pluginClass.Replace("[]", "") + "[...]:",
                                                    new ImageMoniker() { Guid = new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), Id = 127 },
                                                    null));
                        }
                        else
                        {
                            completions.Add(
                                new Completion3(pluginClass.TrimStart('.'),
                                                    modifiersAsString + prefix + pluginClass.TrimStart('.'),
                                                    null,
                                                    new ImageMoniker() { Guid = new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), Id = 127 },
                                                    null));
                        }
                    }
                }
            }

            var completionsToAddToEnd = new List<Completion>();
            foreach (var modifier in completionUtils.Modifiers)
            {
                if (modifiers.Contains(modifier) == false)
                {
                    if (modifier.EndsWith("[]"))
                    {
                        completions.Add(
                            new Completion(modifier.Replace("[]", "[...]:"),
                                                modifiersAsString + modifier + ":",
                                                modifier.Replace("[]", "[...]:"),
                                                completionUtils.TailwindLogo,
                                                null));

                        completionsToAddToEnd.Add(
                            new Completion("group-" + modifier.Replace("[]", "[...]:"),
                                                modifiersAsString + "group-" + modifier + ":",
                                                "group-" + modifier.Replace("[]", "[...]:"),
                                                completionUtils.TailwindLogo,
                                                null));
                    }
                    else
                    {
                        completions.Add(
                            new Completion(modifier + ":",
                                                modifiersAsString + modifier + ":",
                                                modifier,
                                                completionUtils.TailwindLogo,
                                                null));

                        completionsToAddToEnd.Add(
                            new Completion("group-" + modifier + ":",
                                                modifiersAsString + "group-" + modifier + ":",
                                                "group-" + modifier,
                                                completionUtils.TailwindLogo,
                                                null));
                    }
                }
            }

            if (completionUtils.PluginModifiers != null)
            {
                foreach (var modifier in completionUtils.PluginModifiers)
            {
                if (modifiers.Contains(modifier) == false)
                {
                    if (modifier.EndsWith("[]"))
                    {
                        completions.Add(
                            new Completion3(modifier.Replace("[]", "[...]:"),
                                                modifiersAsString + modifier + ":",
                                                modifier.Replace("[]", "[...]:"),
                                                new ImageMoniker() { Guid = new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), Id = 127 },
                                                null));
                    }
                    else
                    {
                        completions.Add(
                            new Completion3(modifier + ":",
                                                modifiersAsString + modifier + ":",
                                                modifier,
                                                new ImageMoniker() { Guid = new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), Id = 127 },
                                                null));
                    }
                }
            }
            }

            foreach (var screen in completionUtils.Screen)
            {
                if (modifiers.Contains(screen) == false)
                {
                    completions.Add(
                        new Completion(screen + ":",
                                            modifiersAsString + screen + ":",
                                            screen,
                                            completionUtils.TailwindLogo,
                                            null));

                    completionsToAddToEnd.Add(
                        new Completion("max-" + screen + ":",
                                            modifiersAsString + "max-" + screen + ":",
                                            screen,
                                            completionUtils.TailwindLogo,
                                            null));

                }
            }

            // this is to keep a decent order within the completion list
            completions.AddRange(completionsToAddToEnd);
            return completions;
        }
    }
}
