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
            var currentClass = classRaw.Split(new string[] { "::" }, StringSplitOptions.None)[0];
            var modifiers = currentClass.Split(':').ToList();

            currentClass = modifiers.Last();
            modifiers.RemoveAt(modifiers.Count - 1);

            var completions = new List<Completion>();

            var modifiersAsString = string.Join(":", modifiers);
            if (string.IsNullOrWhiteSpace(modifiersAsString) == false)
            {
                modifiersAsString += ":";
            }
            var segments = currentClass.Split('-');

            if (modifiers.Count != 0 && modifiers.Any(m => ((m.StartsWith("[") && m.EndsWith("]")) || completionUtils.Modifiers.Contains(m)) == false))
            {
                return null;
            }

            // Prevent Intellisense from showing up for invalid statements like px-0:
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
                        IEnumerable<string> colors = completionUtils.ColorToRgbMapper.Keys;
                        if (twClass.Name.StartsWith(currentClassStem) == false)
                        {
                            // If you're typing in 'tra' to get transform, you don't need a bunch of
                            // completions saying bg-neutral
                            colors = colors.Where(c => c.StartsWith(currentClassStem));
                        }

                        foreach (var color in colors)
                        {
                            var className = string.Format(twClass.Name, color);
                            completions.Add(
                                            new Completion(modifiersAsString + className,
                                                                modifiersAsString + className,
                                                                completionUtils.GetColorDescription(color) ?? className,
                                                                completionUtils.GetImageFromColor(color, color == "transparent" ? 0 : 100),
                                                                null));
                            if (twClass.UseOpacity && currentClass.Contains('/'))
                            {
                                foreach (var opacity in completionUtils.Opacity)
                                {

                                    completions.Add(
                                                new Completion(modifiersAsString + className + $"/{opacity}",
                                                                    modifiersAsString + className + $"/{opacity}",
                                                                    completionUtils.GetColorDescription(color, opacity) ?? className,
                                                                    completionUtils.GetImageFromColor(color, opacity),
                                                                    null));
                                }
                                completions.Add(
                                            new Completion(modifiersAsString + className + "/[...]",
                                                                modifiersAsString + className + "/[]",
                                                                className + "/[...]",
                                                                completionUtils.TailwindLogo,
                                                                null));
                            }


                        }
                    }
                    else if (twClass.UseSpacing)
                    {
                        foreach (var spacing in completionUtils.Spacing)
                        {
                            var className = string.Format(twClass.Name, spacing);

                            completions.Add(
                                new Completion(modifiersAsString + className,
                                                    modifiersAsString + className,
                                                    className,
                                                    completionUtils.TailwindLogo,
                                                    null));
                        }
                    }
                    else if (twClass.SupportsBrackets)
                    {
                        var className = twClass.Name + "-";

                        completions.Add(
                        new Completion(modifiersAsString + className + "[...]",
                                            modifiersAsString + className + "[]",
                                            className + "[...]",
                                            completionUtils.TailwindLogo,
                                            null));
                    }
                    else
                    {
                        completions.Add(
                        new Completion(modifiersAsString + twClass.Name,
                                            modifiersAsString + twClass.Name,
                                            twClass.Name,
                                            completionUtils.TailwindLogo,
                                            null));
                    }
                }
            }

            foreach (var modifier in completionUtils.Modifiers)
            {
                if (modifiers.Contains(modifier) == false)
                {
                    completions.Add(
                        new Completion(modifiersAsString + modifier,
                                            modifiersAsString + modifier + ":",
                                            modifier,
                                            completionUtils.TailwindLogo,
                                            null));
                }
            }

            return completions;
        }
    }
}
