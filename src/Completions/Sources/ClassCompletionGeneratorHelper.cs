using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Package;
using System;
using System.Collections.Generic;
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
                var scope = completionUtils.Classes.Where(
                    c => c.Name.Contains(searchClass) || c.Name.StartsWith(currentClass) || c.UseColors);

                if (currentClass.StartsWith("-") == false)
                {
                    scope = scope.OrderBy(c => c.Name.StartsWith("-"));
                }

                foreach (var twClass in scope)
                {
                    if (twClass.UseColors)
                    {
                        IEnumerable<string> colors;
                        var useCustom = false;

                        if (completionUtils.CustomColorMappers != null && completionUtils.CustomColorMappers.ContainsKey(twClass.Name))
                        {
                            colors = completionUtils.CustomColorMappers[twClass.Name].Keys;
                            useCustom = true;
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
                            if (useCustom)
                            {
                                completions.Add(
                                                new Completion(className,
                                                                    modifiersAsString + className,
                                                                    completionUtils.GetCustomColorDescription(twClass.Name, color) ?? className,
                                                                    completionUtils.GetCustomImageFromColor(twClass.Name, color, color == "transparent" ? 0 : 100),
                                                                    null));
                            }
                            else
                            {
                                completions.Add(
                                                new Completion(className,
                                                                    modifiersAsString + className,
                                                                    completionUtils.GetColorDescription(color) ?? className,
                                                                    completionUtils.GetImageFromColor(color, color == "transparent" ? 0 : 100),
                                                                    null));
                            }
                            if (twClass.UseOpacity && currentClass.Contains('/'))
                            {
                                foreach (var opacity in completionUtils.Opacity)
                                {
                                    if (useCustom)
                                    {
                                        completions.Add(
                                                new Completion(className + $"/{opacity}",
                                                                    modifiersAsString + className + $"/{opacity}",
                                                                    completionUtils.GetCustomColorDescription(twClass.Name, color, opacity) ?? className,
                                                                    completionUtils.GetCustomImageFromColor(twClass.Name, color, opacity),
                                                                    null));
                                    }
                                    else
                                    {
                                        completions.Add(
                                                new Completion(className + $"/{opacity}",
                                                                    modifiersAsString + className + $"/{opacity}",
                                                                    completionUtils.GetColorDescription(color, opacity) ?? className,
                                                                    completionUtils.GetImageFromColor(color, opacity),
                                                                    null));
                                    }
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
                        var spacings = completionUtils.Spacing;

                        if (completionUtils.CustomSpacings != null && completionUtils.CustomSpacings.TryGetValue(twClass.Name, out var value))
                        {
                            spacings = value;
                        }

                        foreach (var spacing in spacings)
                        {
                            var className = string.Format(twClass.Name, spacing);

                            completions.Add(
                                new Completion(className,
                                                    modifiersAsString + className,
                                                    className,
                                                    completionUtils.TailwindLogo,
                                                    null));
                        }
                    }
                    else if (twClass.SupportsBrackets)
                    {
                        completions.Add(
                        new Completion(twClass.Name + "[...]",
                                            modifiersAsString + twClass.Name + "[]",
                                            twClass.Name + "[...]",
                                            completionUtils.TailwindLogo,
                                            null));
                    }
                    else
                    {
                        completions.Add(
                        new Completion(twClass.Name,
                                            modifiersAsString + twClass.Name,
                                            twClass.Name,
                                            completionUtils.TailwindLogo,
                                            null));
                    }
                }
            }

            var completionsToAddToEnd = new List<Completion>();
            foreach (var modifier in completionUtils.Modifiers)
            {
                if (modifiers.Contains(modifier) == false)
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
                                            modifier,
                                            completionUtils.TailwindLogo,
                                            null));

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
