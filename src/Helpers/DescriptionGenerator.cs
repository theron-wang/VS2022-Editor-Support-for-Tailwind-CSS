﻿using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Helpers;

[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class DescriptionGenerator : IDisposable
{
    private readonly SettingsProvider _settingsProvider;
    private readonly Dictionary<string, string> _arbitraryDescriptionCache = [];
    private readonly Dictionary<string, string> _colorDescriptionMapper = [];

    private readonly Regex _stemSplitter = new(@"-(?=(?:[^\[\]()]*[\[\(][^\[\]()]*[\]\)])*[^\[\]()]*$)", RegexOptions.Compiled);

    [ImportingConstructor]
    public DescriptionGenerator(SettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;

        _settingsProvider.OnSettingsChanged += OnSettingsChangedAsync;
    }

    /// <summary>
    /// Private method to format descriptions (convert from single line to multi-line)
    /// </summary>
    /// <param name="text">The text to format</param>
    /// <returns>The formatted CSS description</returns>
    private string FormatDescription(string text, ProjectCompletionValues projectCompletionValues)
    {
        if (text is null)
        {
            return null;
        }

        var index = 0;

        if (projectCompletionValues.Version == TailwindVersion.V4)
        {
            var resultText = new StringBuilder(text);
            int offset = 0;
            int varIndex;

            while ((varIndex = text.IndexOf("var(--", index)) != -1)
            {
                var endParen = text.IndexOf(')', varIndex);
                var semicolon = text.IndexOf(';', varIndex);

                if (endParen == -1 || semicolon == -1)
                {
                    index++;
                    continue;
                }

                var start = varIndex + 4;

                var variable = text.Substring(start, endParen - start);
                index = semicolon;

                string variableValue;

                if (variable == "--spacing")
                {
                    if (projectCompletionValues.CssVariables.TryGetValue("--spacing", out var spacing))
                    {
                        var multiply = text.IndexOf('*', endParen);
                        if (multiply == -1 || multiply > semicolon)
                        {
                            variableValue = spacing;
                        }
                        else
                        {
                            var multiplier = text.Substring(multiply + 1, semicolon - multiply - 1).TrimEnd(')').Trim();

                            if (!double.TryParse(multiplier, out var multiplierAsDouble))
                            {
                                multiplierAsDouble = 1;
                            }

                            var firstNonNumeric = spacing.FirstOrDefault(c => !char.IsDigit(c) && c != '.');
                            // Split between 1rem (1 - rem), 1px (1 - px), etc.
                            var split = spacing.IndexOf(firstNonNumeric);

                            if (split > -1)
                            {
                                var numeric = spacing.Substring(0, split);
                                var nonNumeric = spacing.Substring(split);

                                if (float.TryParse(numeric, out var spacingValue))
                                {
                                    if (nonNumeric == "rem")
                                    {
                                        variableValue = $"{spacingValue * multiplierAsDouble}rem = {spacingValue * multiplierAsDouble * 16}px";
                                    }
                                    else
                                    {
                                        variableValue = $"{spacingValue * multiplierAsDouble}{nonNumeric}";
                                    }
                                }
                                else
                                {
                                    variableValue = spacing;
                                }
                            }
                            else
                            {
                                variableValue = spacing;
                            }
                        }
                    }
                    else
                    {
                        variableValue = null;
                    }
                }
                else if (variable.StartsWith("--color"))
                {
                    var color = variable.Substring(8);

                    if (projectCompletionValues.ColorMapper.TryGetValue(color, out var value))
                    {
                        // If defined in config file, the extension converts to r,g,b format
                        var potentialRgbs = value.Split(',');

                        if (potentialRgbs.Length == 3 && potentialRgbs.All(val => int.TryParse(val, out _)))
                        {
                            value = $"rgb({string.Join(" ", potentialRgbs)})";
                        }

                        if (!_colorDescriptionMapper.TryGetValue(color, out var hex))
                        {
                            hex = ColorHelpers.ConvertToHex(value);
                            _colorDescriptionMapper[color] = hex;
                        }

                        if (string.IsNullOrWhiteSpace(hex) || hex == value)
                        {
                            variableValue = value;
                        }
                        else
                        {
                            variableValue = $"{value} = {hex}";
                        }
                    }
                    else
                    {
                        variableValue = null;
                    }
                }
                else if (!projectCompletionValues.CssVariables.TryGetValue(variable, out variableValue))
                {
                    variableValue = null;
                }

                if (variableValue is not null)
                {
                    var insert = $" /* {variableValue} */";
                    resultText.Insert(semicolon + offset, insert);
                    offset += insert.Length;
                    index = endParen;
                }
                else
                {
                    index++;
                }
            }
            text = resultText.ToString();
        }

        var lines = text.Split([';'], StringSplitOptions.RemoveEmptyEntries);

        var output = new StringBuilder();
        foreach (var line in lines)
        {
            // {0} handle for opacity
            if (line.Contains('{') && line.IndexOf("{0}") != line.IndexOf('{'))
            {
                output.AppendLine($"{line.Split('{')[0].Trim()} {{");
                output.AppendLine($"{line.Split('{')[1].Trim()};");
                continue;
            }
            else if (line.Contains('}') && line.IndexOf("{0}") != line.IndexOf('}') - 2)
            {
                output.AppendLine("}");
                continue;
            }

            output.AppendLine($"{line.Trim()};");
        }
        return output.ToString().Trim();
    }

    /// <summary>
    /// Gets the description for a Tailwind CSS class
    /// </summary>
    /// <param name="text">The unprocessed text: <c>hover:bg-green-800/90</c>, <c>min-w-[10px]</c></param>
    /// <param name="shouldFormat">true iff the description should be formatted</param>
    /// <returns>The description for the given class</returns>
    internal string GetDescription(string text, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        text = text.Split(':').Last();

        if (string.IsNullOrWhiteSpace(projectCompletionValues.Prefix) == false)
        {
            if (text.StartsWith(projectCompletionValues.Prefix))
            {
                text = text.Substring(projectCompletionValues.Prefix.Length);
            }
            else if (text.StartsWith($"-{projectCompletionValues.Prefix}"))
            {
                text = $"-{text.Substring(projectCompletionValues.Prefix.Length + 1)}";
            }
            else
            {
                return "";
            }
        }

        if (ImportantModifierHelper.IsImportantModifier(text))
        {
            text = text.TrimStart('!');
        }

        var description = GetDescriptionForClassOnly(text, projectCompletionValues, shouldFormat: shouldFormat);
        if (string.IsNullOrEmpty(description) == false)
        {
            return description;
        }

        var endsWithArbitrary = text.LastIndexOf('[');
        var segmentText = text;

        if (endsWithArbitrary != -1)
        {
            segmentText = text.Substring(0, endsWithArbitrary);
        }

        var segments = _stemSplitter.Split(segmentText).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        if (endsWithArbitrary != -1)
        {
            segments.Add(text.Substring(endsWithArbitrary).Replace('_', ' '));
        }

        if (segments.Count >= 2)
        {
            string color;
            if (segments.Count >= 3)
            {
                color = string.Join("-", segments.Skip(segments.Count - 2));
            }
            else
            {
                color = segments[segments.Count - 1];
            }
            var stem = text.Replace(color.Replace(' ', '_'), "{0}");

            var opacityText = color.Split('/').Last();
            int? opacity = null;

            if (opacityText != color)
            {
                color = color.Replace($"/{opacityText}", "");
                if (int.TryParse(opacityText, out var o))
                {
                    opacity = o;
                }
            }

            description = GetDescriptionForColorClass(stem, color, opacity: opacity != null, projectCompletionValues, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                if (opacity != null)
                {
                    description = description.Replace("{0}", (projectCompletionValues.Version == TailwindVersion.V3 ? opacity.Value / 100f : opacity.Value).ToString());
                }

                return description;
            }

            var last = segments.Last();
            stem = text.Replace(last.Replace(' ', '_'), "{0}");

            description = GetDescriptionForSpacingClass(stem, last, projectCompletionValues, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                return description;
            }
            
            description = GetDescriptionForNumericClass(stem, last, projectCompletionValues, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                return description;
            }

            description = GetDescriptionForNumericClass(stem, last, projectCompletionValues, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                return description;
            }

            description = GetDescriptionForParenthesisClass(stem.Replace("{0}", "{a}"), last, projectCompletionValues, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                return description;
            }

            return GetDescriptionForArbitraryClass(stem.Replace("-{0}", projectCompletionValues.Version == TailwindVersion.V3 ? "" : "-{a}"), last, projectCompletionValues, shouldFormat: shouldFormat);
        }

        return null;
    }

    /// <summary>
    /// For Tailwind V3, returns an array of the media queries and the total modifier (i.e. &amp;[open]:hover) as the last element.
    /// <br />
    /// For Tailwind V4, returns an array of length one containing the total modifier, with a {0} placeholder for the class description.
    /// </summary>
    /// <param name="modifiersAsString"></param>
    /// <param name="projectCompletionValues"></param>
    /// <returns></returns>
    internal string[] GetTotalModifierDescription(string modifiersAsString, ProjectCompletionValues projectCompletionValues)
    {
        var modifiers = Regex.Split(modifiersAsString, ":(?![^\\[]*\\])");

        var modifierDescriptions = modifiers.Select(m => GetModifierDescription(m, projectCompletionValues, false));

        if (projectCompletionValues.Version == TailwindVersion.V3)
        {
            var nonMediaModifiers = modifierDescriptions
                .Where(d => !string.IsNullOrWhiteSpace(d) && !d.StartsWith("@"));

            var mediaModifiers = modifierDescriptions
                .Where(d => !string.IsNullOrWhiteSpace(d) && d.StartsWith("@"));

            var strippedSelectors = nonMediaModifiers.Select(s => s.TrimStart('&')).ToList();

            var selectors = nonMediaModifiers.Where(s => !s.StartsWith("@") && s.Contains(' ')).ToList();
            var attributes = strippedSelectors.Where(s => s.Contains('[') && !s.StartsWith("@") && !s.Contains(' ')).ToList();
            var pseudoClasses = strippedSelectors.Where(s => s.Contains(':') && !s.Contains('[') && !s.StartsWith("@") && !s.Contains(' ')).ToList();

            var modifierTemplate = "&" + string.Join("", attributes) + string.Join("", pseudoClasses);

            string totalModifier;

            if (selectors.Count > 0)
            {
                totalModifier = string.Join(", ", selectors).Replace("&", modifierTemplate);
            }
            else
            {
                totalModifier = modifierTemplate;
            }

            return [.. mediaModifiers, totalModifier];
        }
        else
        {
            var accumulator = "{0}";

            foreach (var modifier in modifierDescriptions)
            {
                if (string.IsNullOrWhiteSpace(modifier))
                {
                    continue;
                }
                accumulator = accumulator.Replace("{0}", modifier);
            }

            return [accumulator];
        }
    }

    internal string GetModifierDescription(string modifier, ProjectCompletionValues projectCompletionValues, bool trim = true)
    {
        if (projectCompletionValues.VariantsToDescriptions.Count > 0)
        {
            if (projectCompletionValues.VariantsToDescriptions.TryGetValue(modifier, out var description))
            {
                if (trim)
                {
                    return description.Replace("{ {0} }", "").Replace(" {0}", "");
                }
                else
                {
                    return description;
                }
            }
            return null;
        }

        if (modifier.StartsWith("peer-"))
        {
            return $"{GetModifierDescription(modifier.Substring(5), projectCompletionValues).Replace("&", ".peer")} ~ &";
        }
        else if (modifier.StartsWith("group-"))
        {
            return $"{GetModifierDescription(modifier.Substring(6), projectCompletionValues).Replace("&", ".group")} &";
        }
        else if (modifier.StartsWith("[") && modifier.EndsWith("]"))
        {
            return modifier;
        }
        // Special cases
        else if (modifier == "*")
        {
            return "& > *";
        }
        else if (modifier == "first-letter" || modifier == "first-line" || modifier == "placeholder" || modifier == "backdrop" || modifier == "before" ||
                 modifier == "after" || modifier == "marker" || modifier == "selection")
        {
            modifier = $":{modifier}";
        }
        else if (modifier == "file")
        {
            modifier = ":file-selector-button";
        }
        else if (modifier == "even" || modifier == "odd")
        {
            modifier = $":nth-child({modifier})";
        }
        else if (modifier == "open")
        {
            return "&[open]";
        }
        else if (modifier.StartsWith("aria") || modifier.StartsWith("data"))
        {
            if (modifier.Contains('[') || modifier.Contains(']'))
            {
                if (!modifier.EndsWith("]"))
                {
                    return "";
                }
                // arbitrary; return as is without brackets
                return $"&[{modifier.Replace("[", "").Replace("]", "")}=\"true\"]";
            }
            else
            {
                return $"&[{modifier}=\"true\"]";
            }
        }
        else if (modifier == "rtl" || modifier == "ltr")
        {
            return $"&:where([dir=\"{modifier}\"], [dir=\"{modifier}\"] *)";
        }
        else if (modifier.StartsWith("has"))
        {
            if (modifier.Contains('[') && modifier.EndsWith("]"))
            {
                // has-[ length is 5, remove one extra ] at the end
                var inner = modifier.Substring(5, modifier.Length - 6);

                return $"&:has({inner})";
            }

            return "";
        }
        // @ media queries
        else if (modifier.StartsWith("supports"))
        {
            if (modifier.Contains('[') && modifier.EndsWith("]"))
            {
                // supports-[ length is 10, remove one extra ] at the end
                var inner = modifier.Substring(10, modifier.Length - 11);

                if (inner.Contains(':'))
                {
                    return $"@supports ({inner})";
                }
                else
                {
                    return $"@supports ({inner}: var(--tw))";
                }
            }

            return "";
        }
        else if (modifier == "motion-safe")
        {
            return "@media (prefers-reduced-motion: no-preference)";
        }
        else if (modifier == "motion-reduce")
        {
            return "@media (prefers-reduced-motion: reduce)";
        }
        else if (modifier == "contrast-more")
        {
            return "@media (prefers-contrast: more)";
        }
        else if (modifier == "contrast-less")
        {
            return "@media (prefers-contrast: less)";
        }
        else if (modifier == "portrait" || modifier == "landscape")
        {
            return $"@media (orientation: {modifier})";
        }
        else if (modifier == "dark")
        {
            return "@media (prefers-color-scheme: dark)";
        }
        else if (modifier == "forced-colors")
        {
            return "@media (forced-colors: active)";
        }
        else if (modifier == "print")
        {
            return "@media print";
        }
        else if (modifier.StartsWith("min-"))
        {
            return $"@media (min-width: {modifier.Substring(5).TrimEnd(']')})";
        }
        else if (modifier.StartsWith("max-["))
        {
            if (modifier.EndsWith("]"))
            {
                return $"@media (min-width: {modifier.Substring(5).TrimEnd(']')})";
            }

            return "";
        }
        else if (projectCompletionValues.Screen.Contains(modifier) || projectCompletionValues.Screen.Contains($"max-{modifier}"))
        {
            // TODO: handle screens
            return "";
        }

        return $"&:{modifier}";
    }

    private string GetDescriptionForClassOnly(string tailwindClass, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        string description = null;
        if (projectCompletionValues.CustomDescriptionMapper.ContainsKey(tailwindClass))
        {
            description = projectCompletionValues.CustomDescriptionMapper[tailwindClass];
        }
        else if (projectCompletionValues.DescriptionMapper.ContainsKey(tailwindClass))
        {
            description = projectCompletionValues.DescriptionMapper[tailwindClass];
        }

        if (description == null)
        {
            return null;
        }

        var oldDescription = description;
        try
        {
            var index = 0;
            while (description.IndexOf("rem;", index) != -1)
            {
                var remIndex = description.IndexOf("rem;", index);

                var replace = description.Substring(remIndex, 4);

                var start = description.LastIndexOf(' ', remIndex) + 1;
                var number = float.Parse(description.Substring(start, remIndex - start));

                replace = $"{number}{replace}";

                description = description.Replace(replace, $"{number}rem /*{(tailwindClass.StartsWith("-") ? -1 : 1) * number * 16}px*/;");
                index = description.IndexOf("*/", remIndex);
            }

            index = 0;
            while (description.IndexOf("rgb", index) != -1)
            {
                var rgbIndex = description.IndexOf("rgb(", index);

                var insert = description.IndexOf(';', rgbIndex);

                index = rgbIndex + 1;

                if (insert == -1)
                {
                    continue;
                }

                var values = description.Substring(rgbIndex, insert - rgbIndex).Split();

                if (values.Length < 3 || values.Take(3).Any(v => !int.TryParse(v, out _)))
                {
                    continue;
                }

                var rgbValues = values.Take(3).Select(int.Parse).ToArray();

                var hex = $"#{rgbValues[0]:X}{rgbValues[1]:X}{rgbValues[2]:X}";

                description = description.Insert(insert + 1, $" /*{hex}*/");

                index = description.IndexOf(';', insert);
            }
        }
        catch
        {
            description = oldDescription;
        }

        if (shouldFormat)
        {
            return FormatDescription(description, projectCompletionValues);
        }
        return description;
    }

    private string GetDescriptionForSpacingClass(string tailwindClass, string spacing, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        var negative = tailwindClass.StartsWith("-");
        string spacingValue;

        if (spacing[0] == '[' && spacing[spacing.Length - 1] == ']')
        {
            spacingValue = spacing.Substring(1, spacing.Length - 2);

            if (string.IsNullOrWhiteSpace(spacingValue))
            {
                return null;
            }

            if (spacingValue.StartsWith("--"))
            {
                spacingValue = $"var({spacingValue})";
            }

            if (negative)
            {
                spacingValue = $"calc({spacingValue} * -1)";
            }
        }
        else if (spacing[0] == '(' && spacing[spacing.Length - 1] == ')')
        {
            spacingValue = spacing.Substring(1, spacing.Length - 2);

            if (string.IsNullOrWhiteSpace(spacingValue) || !spacingValue.StartsWith("--"))
            {
                return null;
            }

            spacingValue = $"var({spacingValue})";

            if (negative)
            {
                spacingValue = $"calc({spacingValue} * -1)";
            }
        }
        else if (projectCompletionValues.Version == TailwindVersion.V4)
        {
            if (double.TryParse(spacing, out _))
            {
                spacing = $"{(negative ? "-" : "")}{spacing}";

                spacingValue = $"calc(var(--spacing) * {spacing})";
            }
            else if (spacing == "px")
            {
                spacingValue = $"{(negative ? "-" : "")}1{spacing}";
            }
            else
            {
                return null;
            }
        }
        else
        {
            if (projectCompletionValues.CustomSpacingMappers.TryGetValue(tailwindClass, out var dict))
            {
                if (dict.TryGetValue(spacing, out var val))
                {
                    spacingValue = val.Trim();
                }
                else
                {
                    return null;
                }
            }
            else if (projectCompletionValues.SpacingMapper.TryGetValue(spacing, out spacingValue) == false)
            {
                return null;
            }
        }

        if (spacingValue.EndsWith("rem") && double.TryParse(spacingValue.Replace("rem", ""), out var result))
        {
            spacingValue += $" /*{(negative ? -1 : 1) * result * 16}px*/";
        }

        var key = tailwindClass.Replace("{0}", "{s}");

        if (!projectCompletionValues.DescriptionMapper.TryGetValue(key, out var description))
        {
            if (!negative || projectCompletionValues.Version != TailwindVersion.V4)
            {
                return null;
            }

            if (!projectCompletionValues.DescriptionMapper.TryGetValue(key.TrimStart('-'), out description))
            {
                return null;
            }
        }

        if (projectCompletionValues.Version == TailwindVersion.V3)
        {
            if (negative)
            {
                spacingValue = $"-{spacingValue}";
            }
        }

        var format = description.Replace("{0}", spacingValue);
        if (shouldFormat)
        {
            return FormatDescription(format, projectCompletionValues);
        }
        return format;
    }

    private string GetDescriptionForNumericClass(string tailwindClass, string numberFractionOrPercent, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        if (projectCompletionValues.Version != TailwindVersion.V4)
        {
            return null;
        }

        var negative = tailwindClass.StartsWith("-");
        string value = null;
        List<string> types = [];

        if (numberFractionOrPercent[0] == '[' && numberFractionOrPercent[numberFractionOrPercent.Length - 1] == ']')
        {
            value = numberFractionOrPercent.Substring(1, numberFractionOrPercent.Length - 2);

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            types.Add("{n}");
            types.Add("{%}");
            types.Add("{f}");
        }
        else if (numberFractionOrPercent[0] == '(' && numberFractionOrPercent[numberFractionOrPercent.Length - 1] == ')')
        {
            value = numberFractionOrPercent.Substring(1, numberFractionOrPercent.Length - 2);

            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("--"))
            {
                return null;
            }

            value = $"var({value})";
            types.Add("{n}");
            types.Add("{%}");
            types.Add("{f}");
        }
        else if (double.TryParse(numberFractionOrPercent, out _))
        {
            value = numberFractionOrPercent;
            types.Add("{n}");
        }
        else if (numberFractionOrPercent.EndsWith("%") && double.TryParse(numberFractionOrPercent.TrimEnd('%'), out _))
        {
            value = numberFractionOrPercent;
            types.Add("{%}");
        }
        else if (numberFractionOrPercent.Contains('/'))
        {
            var split = numberFractionOrPercent.Split('/');

            if (split.Length == 2 && split.All(s => double.TryParse(s, out _)))
            {
                value = numberFractionOrPercent;
                types.Add("{f}");
            }
        }

        if (value is null || types.Count == 0)
        {
            return null;
        }

        foreach (var type in types)
        {
            var key = tailwindClass.Replace("{0}", type);

            if (!projectCompletionValues.DescriptionMapper.TryGetValue(key, out var description))
            {
                if (!negative || projectCompletionValues.Version != TailwindVersion.V4)
                {
                    continue;
                }

                if (!projectCompletionValues.DescriptionMapper.TryGetValue(key.TrimStart('-'), out description))
                {
                    continue;
                }
            }

            if (negative)
            {
                value = $"calc({value} * -1)";
            }
            var format = description.Replace("{0}", value);
            if (shouldFormat)
            {
                return FormatDescription(format, projectCompletionValues);
            }
            return format;
        }

        return null;
    }

    private string GetDescriptionForColorClass(string tailwindClass, string color, bool opacity, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        if (projectCompletionValues.Version == TailwindVersion.V3)
        {
            var rgbValue = new StringBuilder();

            string hex;

            if (color[0] == '[' && color[color.Length - 1] == ']')
            {
                var c = color.Substring(1, color.Length - 2);
                if (ColorHelpers.IsHex(c, out hex))
                {
                    var rgb = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                    rgbValue.Append($"rgb({rgb.R} {rgb.G} {rgb.B}");
                }
                else if (c.StartsWith("rgb"))
                {
                    rgbValue.Append(c.TrimEnd(')'));
                    hex = GetColorDescription(color, tailwindClass, projectCompletionValues, true);
                }
                else if (c.StartsWith("--"))
                {
                    rgbValue.Append($"rgb(var({c})");
                    hex = $"var({c})";
                }
            }
            else
            {
                rgbValue.Append(GetColorDescription(color, tailwindClass, projectCompletionValues));
                hex = GetColorDescription(color, tailwindClass, projectCompletionValues, true);
            }

            if (string.IsNullOrEmpty(rgbValue.ToString()) || projectCompletionValues.DescriptionMapper.ContainsKey(tailwindClass.Replace("{0}", "{c}")) == false)
            {
                return null;
            }

            var format = new StringBuilder(projectCompletionValues.DescriptionMapper[tailwindClass.Replace("{0}", "{c}")]);
            var formatAsString = format.ToString();

            if (formatAsString.Contains("{0};"))
            {
                if (rgbValue.ToString().StartsWith("{noparse}"))
                {
                    var returnFormat = string.Format(format.ToString(), rgbValue.Replace("{noparse}", ""), hex);
                    if (shouldFormat)
                    {
                        return FormatDescription(returnFormat, projectCompletionValues);
                    }
                    return returnFormat;
                }
                else
                {
                    if (opacity)
                    {
                        format.Replace(": 1;", ": {2};");
                    }

                    var returnFormat = string.Format(format.ToString(), rgbValue + ")", hex, "{0}");
                    if (shouldFormat)
                    {
                        return FormatDescription(returnFormat, projectCompletionValues);
                    }
                    return returnFormat;
                }
            }

            if (rgbValue.ToString().StartsWith("{noparse}"))
            {
                var startIndex = formatAsString.IndexOf("{0}") + 3;
                var replace = formatAsString.Substring(startIndex, formatAsString.IndexOf(';', startIndex) - startIndex);

                format.Replace(replace, "");
                rgbValue.Replace("{noparse}", "");

                var returnFormat = string.Format(format.ToString(), rgbValue.ToString(), hex);
                if (shouldFormat)
                {
                    return FormatDescription(returnFormat, projectCompletionValues);
                }
                return returnFormat;
            }
            else
            {
                if (opacity)
                {
                    format.Replace(": 1;", ": {2};");
                }

                var returnFormat = string.Format(format.ToString(), rgbValue.ToString() + " ", hex, "{0}");

                if (shouldFormat)
                {
                    return FormatDescription(returnFormat, projectCompletionValues);
                }
                return returnFormat;
            }
        }
        else
        {
            // Tailwind v4+
            string colorAttributeValue;

            if (color[0] == '[' && color[color.Length - 1] == ']')
            {
                var c = color.Substring(1, color.Length - 2);

                if (string.IsNullOrWhiteSpace(c))
                {
                    return null;
                }

                colorAttributeValue = c;
            }
            else if (color[0] == '(' && color[color.Length - 1] == ')')
            {
                var c = color.Substring(1, color.Length - 2);

                if (string.IsNullOrWhiteSpace(c) || !c.StartsWith("--"))
                {
                    return null;
                }

                colorAttributeValue = $"var({c})";
            }
            else if (projectCompletionValues.ColorMapper.ContainsKey(color))
            {
                colorAttributeValue = $"var(--color-{color})";
            }
            else
            {
                return null;
            }

            if (!projectCompletionValues.DescriptionMapper.TryGetValue(tailwindClass.Replace("{0}", "{c}"), out var desc))
            {
                return null;
            }

            if (opacity)
            {
                colorAttributeValue = $"color-mix(in oklab, {colorAttributeValue} {{0}}%, transparent)";
            }

            if (shouldFormat)
            {
                return FormatDescription(desc.Replace("{0}", colorAttributeValue), projectCompletionValues);
            }
            else
            {
                return desc.Replace("{0}", colorAttributeValue);
            }
        }
    }

    private string GetDescriptionForArbitraryClass(string stem, string arbitrary, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        // i.e. length in text-[length:2px]
        string special = null;

        if (arbitrary[0] == '[' && arbitrary[arbitrary.Length - 1] == ']')
        {
            arbitrary = arbitrary.Substring(1, arbitrary.Length - 2);

            if (string.IsNullOrWhiteSpace(arbitrary))
            {
                return null;
            }

            if (arbitrary.Contains(':'))
            {
                special = arbitrary.Split(':')[0].Trim();
                arbitrary = arbitrary.Substring(arbitrary.IndexOf(':') + 1).Trim();
            }

            // No spaces possible; unescape _ format
            // Since spaces aren't possible, '\ ' must mean it used to be '\_'
            arbitrary = arbitrary.Replace("_", " ").Replace("\\ ", "_");

            // Replace css variables (starts with --) with var(--...)
            if (arbitrary.StartsWith("--"))
            {
                arbitrary = $"var({arbitrary})";
            }

            if (stem.StartsWith("-"))
            {
                if (projectCompletionValues.Version == TailwindVersion.V3)
                {
                    arbitrary = $"-{arbitrary}";
                }
                else
                {
                    arbitrary = $"calc({arbitrary} * -1)";
                }
            }
        }
        else
        {
            return null;
        }

        if (_arbitraryDescriptionCache.TryGetValue($"{stem}-[{special}]", out var description))
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            if (shouldFormat)
            {
                return FormatDescription(description.Replace("{0}", arbitrary), projectCompletionValues);
            }
            return description.Replace("{0}", arbitrary);
        }

        if (special is not null && projectCompletionValues.Version == TailwindVersion.V4)
        {
            if (stem == "text-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "font-size: {0};";
            }
            else if (stem == "font-{a}" && special == "family-name")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "font-family: {0};";
            }
            else if (stem == "bg-{a}" && special == "image")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "background-image: {0};";
            }
            else if (stem == "bg-{a}" && special == "position")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "background-position: {0};";
            }
            else if (stem == "bg-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "background-size: {0};";
            }
            else if (stem == "border-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-width: {0};";
            }
            else if (stem == "border-x-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-inline-width: {0};";
            }
            else if (stem == "border-y-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-block-width: {0};";
            }
            else if (stem == "border-s-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-inline-start-width: {0};";
            }
            else if (stem == "border-e-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-inline-end-width: {0};";
            }
            else if (stem == "border-t-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-top-width: {0};";
            }
            else if (stem == "border-r-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-right-width: {0};";
            }
            else if (stem == "border-b-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-bottom-width: {0};";
            }
            else if (stem == "border-l-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "border-left-width: {0};";
            }
            else if (stem == "divide-x-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "& > :not(:last-child) { border-inline-start-width: 0px; border-inline-end-width: {0}; }";
            }
            else if (stem == "divide-y-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "& > :not(:last-child) { border-top-width: 0px; border-bottom-width: {0}; }";
            }
            else if (stem == "outline-{a}" && special == "length")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "outline-width: {0};";
            }
            else if (stem == "shadow-{a}" && special == "color")
            {
                _arbitraryDescriptionCache[$"{stem}-[{special}]"] = "--tw-shadow-color: {0};";
            }
            else
            {
                return null;
            }

            description = _arbitraryDescriptionCache[$"{stem}-[{special}]"];

            if (shouldFormat)
            {
                return FormatDescription(description.Replace("{0}", arbitrary), projectCompletionValues);
            }
            return description.Replace("{0}", arbitrary);
        }

        if (projectCompletionValues.DescriptionMapper.TryGetValue(stem.TrimStart('-'), out description))
        {
            if (shouldFormat)
            {
                return FormatDescription(description.Replace("{0}", arbitrary), projectCompletionValues);
            }
            return description.Replace("{0}", arbitrary);
        }

        var existingDescriptions = projectCompletionValues.DescriptionMapper
            .Concat(projectCompletionValues.CustomDescriptionMapper)
            .Where(kv => kv.Key.StartsWith(stem) && kv.Key.Replace(stem, "").Contains('-'))
            .Take(2)
            .Select(kv => kv.Value.Split([';'], StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        // If the descriptions have different css attributes, return null
        if (existingDescriptions.Count != 2 || existingDescriptions[0].Length != existingDescriptions[1].Length)
        {
            _arbitraryDescriptionCache[$"{stem}-[]"] = null;
            return null;
        }

        // Find the different values between the two descriptions and replace them with {0}
        // Otherwise, just add the same thing
        var returnFormat = new StringBuilder();

        for (int i = 0; i < existingDescriptions[0].Length; i++)
        {
            if (existingDescriptions[0][i] == existingDescriptions[1][i])
            {
                returnFormat.Append(existingDescriptions[0][i].Trim() + "; ");
            }
            else
            {
                var attribute1 = existingDescriptions[0][i].Split(':')[0].Trim();
                var attribute2 = existingDescriptions[1][i].Split(':')[0].Trim();

                if (attribute1 != attribute2)
                {
                    _arbitraryDescriptionCache[$"{stem}-[]"] = null;
                    return null;
                }

                returnFormat.Append($"{attribute1}: {{0}}; ");
            }
        }

        description = returnFormat.ToString().Trim();
        _arbitraryDescriptionCache[$"{stem}-[]"] = description;

        if (shouldFormat)
        {
            return FormatDescription(description.Replace("{0}", arbitrary), projectCompletionValues);
        }
        return description.Replace("{0}", arbitrary);
    }

    private string GetDescriptionForParenthesisClass(string stem, string parenthesis, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        if (projectCompletionValues.Version != TailwindVersion.V4)
        {
            return null;
        }

        string special = null;
        if (parenthesis[0] == '(' && parenthesis[parenthesis.Length - 1] == ')')
        {
            parenthesis = parenthesis.Substring(1, parenthesis.Length - 2);

            if (parenthesis.Contains(':'))
            {
                special = parenthesis.Split(':')[0].Trim();

                parenthesis = parenthesis.Substring(parenthesis.IndexOf(':') + 1).Trim();
            }

            if (string.IsNullOrWhiteSpace(parenthesis) || !parenthesis.StartsWith("--"))
            {
                return null;
            }

            parenthesis = $"var({parenthesis})";
        }
        else
        {
            return null;
        }

        if (special is null)
        {
            return GetDescriptionForArbitraryClass(stem, parenthesis, projectCompletionValues, shouldFormat);
        }
        else
        {
            return GetDescriptionForArbitraryClass(stem, $"{special}:{parenthesis}", projectCompletionValues, shouldFormat);
        }
    }

    private string GetColorDescription(string color, string stem, ProjectCompletionValues projectCompletionValues, bool hex = false)
    {
        string value;
        Dictionary<string, string> dict = null;

        if (stem != null)
        {
            if (projectCompletionValues.CustomColorMappers != null && projectCompletionValues.CustomColorMappers.TryGetValue(stem, out dict) == false)
            {
                if (_colorDescriptionMapper.TryGetValue($"{hex}/{color}", out value))
                {
                    return value;
                }

                if (projectCompletionValues.ColorMapper.TryGetValue(color, out value) == false)
                {
                    return null;
                }

                if (_colorDescriptionMapper.TryGetValue($"{hex}/{stem}/{color}", out var desc))
                {
                    return desc;
                }
            }
            else if (dict != null && dict.TryGetValue(color, out value))
            {
                if (projectCompletionValues.ColorMapper.TryGetValue(color, out var value2) && value == value2)
                {
                    if (projectCompletionValues.ColorMapper.TryGetValue(color, out value) == false)
                    {
                        return null;
                    }
                }
            }
            // Has override, but current color is not included
            else
            {
                return null;
            }
        }
        else
        {
            if (_colorDescriptionMapper.TryGetValue($"{hex}/{color}", out value))
            {
                return value;
            }
            if (projectCompletionValues.ColorMapper.TryGetValue(color, out value) == false)
            {
                return null;
            }
        }
        // Invalid color or value is empty when color is current, inherit, or transparent
        if (string.IsNullOrWhiteSpace(value))
        {
            return color;
        }
        else if (value.StartsWith("{noparse}"))
        {
            return value;
        }

        var rgb = value.Split(',');

        if (rgb.Length == 0)
        {
            return null;
        }

        string result;
        if (hex)
        {
            result = $"#{int.Parse(rgb[0]):X2}{int.Parse(rgb[1]):X2}{int.Parse(rgb[2]):X2}";
        }
        else
        {
            result = $"rgb({rgb[0]} {rgb[1]} {rgb[2]}";
        }

        var key = $"{hex}/{color}";
        if (projectCompletionValues.CustomColorMappers.ContainsKey(stem))
        {
            key = $"{hex}/{stem}/{key}";
        }

        _colorDescriptionMapper[key] = result;

        return result;
    }

    private Task OnSettingsChangedAsync(TailwindSettings settings)
    {
        _colorDescriptionMapper.Clear();
        _arbitraryDescriptionCache.Clear();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _settingsProvider.OnSettingsChanged -= OnSettingsChangedAsync;
    }
}
