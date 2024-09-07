using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Helpers;

[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class DescriptionGenerator
{
    [Import]
    public CompletionUtilities CompletionUtilities { get; set; }

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
    internal string GetDescription(string text, bool shouldFormat = true)
    {
        text = text.Split(':').Last();

        if (string.IsNullOrWhiteSpace(CompletionUtilities.Prefix) == false && text.StartsWith(CompletionUtilities.Prefix))
        {
            text = text.Substring(CompletionUtilities.Prefix.Length);
        }

        var description = GetDescriptionForClassOnly(text, shouldFormat: shouldFormat);
        if (string.IsNullOrEmpty(description) == false)
        {
            return description;
        }

        var segments = text.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2)
        {
            string color;
            if (segments.Length >= 3)
            {
                color = string.Join("-", segments.Skip(1));
            }
            else
            {
                color = segments[segments.Length - 1];
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

            description = GetDescriptionForColorClass(stem, color, opacity: opacity != null, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                if (opacity != null)
                {
                    description = string.Format(description, opacity.Value / 100f);
                }

                return description;
            }

            var spacing = segments.Last();
            stem = text.Replace(spacing, "{0}");

            return GetDescriptionForSpacingClass(stem, spacing, shouldFormat: shouldFormat);
        }

        return null;
    }

    private string GetDescriptionForClassOnly(string tailwindClass, bool shouldFormat = true)
    {
        string description = null;
        if (CompletionUtilities.CustomDescriptionMapper.ContainsKey(tailwindClass))
        {
            description = CompletionUtilities.CustomDescriptionMapper[tailwindClass];
        }
        else if (CompletionUtilities.DescriptionMapper.ContainsKey(tailwindClass))
        {
            description = CompletionUtilities.DescriptionMapper[tailwindClass];
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

    private string GetDescriptionForSpacingClass(string tailwindClass, string spacing, bool shouldFormat = true)
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
        }
        else if (CompletionUtilities.CustomSpacingMappers.TryGetValue(tailwindClass, out var dict))
        {
            spacingValue = dict[spacing].Trim();
        }
        else if (CompletionUtilities.SpacingMapper.TryGetValue(spacing, out spacingValue) == false)
        {
            return null;
        }

        if (spacingValue.EndsWith("rem") && double.TryParse(spacingValue.Replace("rem", ""), out var result))
        {
            spacingValue += $" /*{(negative ? -1 : 1) * result * 16}px*/";
        }

        var key = tailwindClass.Replace("{0}", "{s}");

        if (CompletionUtilities.DescriptionMapper.ContainsKey(key))
        {
            var format = string.Format(CompletionUtilities.DescriptionMapper[key], (negative ? "-" : "") + spacingValue);
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

    private string GetDescriptionForColorClass(string tailwindClass, string color, bool opacity, bool shouldFormat = true)
    {
        var value = new StringBuilder();

        if (color[0] == '[' && color[color.Length - 1] == ']')
        {
            var c = color.Substring(1, color.Length - 2);
            if (ColorHelpers.IsHex(c, out string hex))
            {
                var rgb = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                value.Append($"rgb({rgb.R} {rgb.G} {rgb.B}");
            }
            else if (c.StartsWith("rgb"))
            {
                value.Append(c.TrimEnd(')'));
            }
        }
        else
        {
            value.Append(GetColorDescription(color, tailwindClass));
        }

        if (string.IsNullOrEmpty(value.ToString()) || CompletionUtilities.DescriptionMapper.ContainsKey(tailwindClass.Replace("{0}", "{c}")) == false)
        {
            return null;
        }

        var format = new StringBuilder(CompletionUtilities.DescriptionMapper[tailwindClass.Replace("{0}", "{c}")]);
        var formatAsString = format.ToString();

        if (formatAsString.Contains("{0};"))
        {
            if (value.ToString().StartsWith("{noparse}"))
            {
                var returnFormat = string.Format(format.ToString(), value.Replace("{noparse}", ""));
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
                    format.Replace(": 1;", ": {1};");
                }

                var returnFormat = string.Format(format.ToString(), value + ")", "{0}");
                if (shouldFormat)
                {
                    return FormatDescription(returnFormat);
                }
                return returnFormat;
            }
        }

        if (value.ToString().StartsWith("{noparse}"))
        {
            var startIndex = formatAsString.IndexOf("{0}") + 3;
            var replace = formatAsString.Substring(startIndex, formatAsString.IndexOf(';', startIndex) - startIndex);

            format.Replace(replace, "");
            value.Replace("{noparse}", "");

            var returnFormat = string.Format(format.ToString(), value.ToString());
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
                format.Replace(": 1;", ": {1};");
            }

            var returnFormat = string.Format(format.ToString(), value.ToString() + " ", "{0}");

            if (shouldFormat)
            {
                return FormatDescription(returnFormat);
            }
            return returnFormat;
        }
    }

    private string GetColorDescription(string color, string stem)
    {
        string value;
        Dictionary<string, string> dict = null;

        if (stem != null)
        {
            if (CompletionUtilities.ColorDescriptionMapper.TryGetValue($"{stem}/{color}", out value))
            {
                return value;
            }
            if (CompletionUtilities.CustomColorMappers != null && CompletionUtilities.CustomColorMappers.TryGetValue(stem, out dict) == false)
            {
                if (CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
                {
                    return null;
                }

                if (CompletionUtilities.ColorDescriptionMapper.TryGetValue(color, out var desc))
                {
                    return desc;
                }
            }
            else if (dict != null && dict.TryGetValue(color, out value))
            {
                if (CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out var value2) && value == value2)
                {
                    if (CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
                    {
                        return null;
                    }
                }
            }
            else if (CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
            {
                return null;
            }
        }
        else
        {
            if (CompletionUtilities.ColorDescriptionMapper.TryGetValue(color, out value))
            {
                return value;
            }
            if (CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
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

        var result = $"rgb({rgb[0]} {rgb[1]} {rgb[2]}";

        var key = color;
        if (CompletionUtilities.CustomColorMappers.ContainsKey(stem))
        {
            key = $"{stem}/{key}";
        }

        CompletionUtilities.ColorDescriptionMapper[key] = result;

        return result;
    }
}
