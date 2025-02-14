﻿using System;
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
    private readonly CompletionUtilities _completionUtilities;
    private readonly SettingsProvider _settingsProvider;
    private readonly Dictionary<string, string> _arbitraryDescriptionCache = [];
    private readonly Dictionary<string, string> _colorDescriptionMapper = [];

    [ImportingConstructor]
    public DescriptionGenerator(CompletionUtilities completionUtilities, SettingsProvider settingsProvider)
    {
        _completionUtilities = completionUtilities;
        _settingsProvider = settingsProvider;

        _settingsProvider.OnSettingsChanged += OnSettingsChangedAsync;
    }

    /// <summary>
    /// Private method to format descriptions (convert from single line to multi-line)
    /// </summary>
    /// <param name="text">The text to format</param>
    /// <returns>The formatted CSS description</returns>
    private string FormatDescription(string text)
    {
        if (text is null)
        {
            return null;
        }

        var lines = text.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        var output = new StringBuilder();
        foreach (var line in lines)
        {
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

        var segments = segmentText.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        if (endsWithArbitrary != -1)
        {
            segments.Add(text.Substring(endsWithArbitrary));
        }

        if (segments.Count >= 2)
        {
            string color;
            if (segments.Count >= 3)
            {
                color = string.Join("-", segments.Skip(1));
            }
            else
            {
                color = segments[segments.Count - 1];
            }
            var stem = text.Replace(color, "{0}");

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
                    description = string.Format(description, opacity.Value / 100f);
                }

                return description;
            }

            var last = segments.Last();
            stem = text.Replace(last, "{0}");

            description = GetDescriptionForSpacingClass(stem, last, projectCompletionValues, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                return description;
            }

            return GetDescriptionForArbitraryClass(stem.Replace("-{0}", ""), last, projectCompletionValues, shouldFormat: shouldFormat);
        }

        return null;
    }

    internal string[] GetTotalModifierDescription(string modifiersAsString, ProjectCompletionValues projectCompletionValues)
    {
        var modifiers = Regex.Split(modifiersAsString, ":(?![^\\[]*\\])");

        var modifierDescriptions = modifiers.Select(m => GetModifierDescription(m, projectCompletionValues));

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

        return [..mediaModifiers, totalModifier];
    }

    internal string GetModifierDescription(string modifier, ProjectCompletionValues projectCompletionValues)
    {
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
            return  $"@media (orientation: {modifier})";
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
            return FormatDescription(description);
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

            if (spacingValue.StartsWith("--"))
            {
                spacingValue = $"var({spacingValue})";
            }

            if (string.IsNullOrWhiteSpace(spacingValue))
            {
                return null;
            }
        }
        else if (projectCompletionValues.CustomSpacingMappers.TryGetValue(tailwindClass, out var dict))
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

        if (spacingValue.EndsWith("rem") && double.TryParse(spacingValue.Replace("rem", ""), out var result))
        {
            spacingValue += $" /*{(negative ? -1 : 1) * result * 16}px*/";
        }

        var key = tailwindClass.Replace("{0}", "{s}");

        if (projectCompletionValues.DescriptionMapper.ContainsKey(key))
        {
            var format = string.Format(projectCompletionValues.DescriptionMapper[key], (negative ? "-" : "") + spacingValue);
            if (shouldFormat)
            {
                return FormatDescription(format);
            }
            return format;
        }
        else
        {
            return null;
        }
    }

    private string GetDescriptionForColorClass(string tailwindClass, string color, bool opacity, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        var rgbValue = new StringBuilder();

        string hex;

        // TODO: add hex equivalent for rgb values in description
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
                    return FormatDescription(returnFormat);
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
                    return FormatDescription(returnFormat);
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
                return FormatDescription(returnFormat);
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
                return FormatDescription(returnFormat);
            }
            return returnFormat;
        }
    }

    private string GetDescriptionForArbitraryClass(string stem, string arbitrary, ProjectCompletionValues projectCompletionValues, bool shouldFormat = true)
    {
        if (arbitrary[0] == '[' && arbitrary[arbitrary.Length - 1] == ']')
        {
            arbitrary = arbitrary.Substring(1, arbitrary.Length - 2);

            if (string.IsNullOrWhiteSpace(arbitrary))
            {
                return null;
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
                arbitrary = $"-{arbitrary}";
            }
        }
        else
        {
            return null;
        }

        if (_arbitraryDescriptionCache.TryGetValue($"{stem}-[]", out var description))
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            if (shouldFormat)
            {
                return FormatDescription(string.Format(description, arbitrary));
            }
            return string.Format(description, arbitrary);
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
            return FormatDescription(string.Format(description, arbitrary));
        }
        return string.Format(description, arbitrary);
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

                if (projectCompletionValues.ColorToRgbMapper.TryGetValue(color, out value) == false)
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
                if (projectCompletionValues.ColorToRgbMapper.TryGetValue(color, out var value2) && value == value2)
                {
                    if (projectCompletionValues.ColorToRgbMapper.TryGetValue(color, out value) == false)
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
            if (projectCompletionValues.ColorToRgbMapper.TryGetValue(color, out value) == false)
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
