using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
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
    internal string GetDescription(string text, bool shouldFormat = true)
    {
        text = text.Split(':').Last();

        if (string.IsNullOrWhiteSpace(_completionUtilities.Prefix) == false)
        {
            if (text.StartsWith(_completionUtilities.Prefix))
            {
                text = text.Substring(_completionUtilities.Prefix.Length);
            }
            else if (text.StartsWith($"-{_completionUtilities.Prefix}"))
            {
                text = $"-{text.Substring(_completionUtilities.Prefix.Length + 1)}";
            }
            else
            {
                return "";
            }
        }

        if (ImportantModiferHelper.IsImportantModifier(text))
        {
            text = text.TrimStart('!');
        }

        var description = GetDescriptionForClassOnly(text, shouldFormat: shouldFormat);
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

            description = GetDescriptionForColorClass(stem, color, opacity: opacity != null, shouldFormat: shouldFormat);

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

            description = GetDescriptionForSpacingClass(stem, last, shouldFormat: shouldFormat);

            if (string.IsNullOrEmpty(description) == false)
            {
                return description;
            }

            return GetDescriptionForArbitraryClass(stem.Replace("-{0}", ""), last, shouldFormat: shouldFormat);
        }

        return null;
    }

    private string GetDescriptionForClassOnly(string tailwindClass, bool shouldFormat = true)
    {
        string description = null;
        if (_completionUtilities.CustomDescriptionMapper.ContainsKey(tailwindClass))
        {
            description = _completionUtilities.CustomDescriptionMapper[tailwindClass];
        }
        else if (_completionUtilities.DescriptionMapper.ContainsKey(tailwindClass))
        {
            description = _completionUtilities.DescriptionMapper[tailwindClass];
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

            if (spacingValue.StartsWith("--"))
            {
                spacingValue = $"var({spacingValue})";
            }

            if (string.IsNullOrWhiteSpace(spacingValue))
            {
                return null;
            }
        }
        else if (_completionUtilities.CustomSpacingMappers.TryGetValue(tailwindClass, out var dict))
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
        else if (_completionUtilities.SpacingMapper.TryGetValue(spacing, out spacingValue) == false)
        {
            return null;
        }

        if (spacingValue.EndsWith("rem") && double.TryParse(spacingValue.Replace("rem", ""), out var result))
        {
            spacingValue += $" /*{(negative ? -1 : 1) * result * 16}px*/";
        }

        var key = tailwindClass.Replace("{0}", "{s}");

        if (_completionUtilities.DescriptionMapper.ContainsKey(key))
        {
            var format = string.Format(_completionUtilities.DescriptionMapper[key], (negative ? "-" : "") + spacingValue);
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
                hex = GetColorDescription(color, tailwindClass, true);
            }
            else if (c.StartsWith("--"))
            {
                rgbValue.Append($"rgb(var({c})");
                hex = $"var({c})";
            }
        }
        else
        {
            rgbValue.Append(GetColorDescription(color, tailwindClass));
            hex = GetColorDescription(color, tailwindClass, true);
        }

        if (string.IsNullOrEmpty(rgbValue.ToString()) || _completionUtilities.DescriptionMapper.ContainsKey(tailwindClass.Replace("{0}", "{c}")) == false)
        {
            return null;
        }

        var format = new StringBuilder(_completionUtilities.DescriptionMapper[tailwindClass.Replace("{0}", "{c}")]);
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

    private string GetDescriptionForArbitraryClass(string stem, string arbitrary, bool shouldFormat = true)
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

        var existingDescriptions = _completionUtilities.DescriptionMapper
            .Concat(_completionUtilities.CustomDescriptionMapper)
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

    private string GetColorDescription(string color, string stem, bool hex = false)
    {
        string value;
        Dictionary<string, string> dict = null;

        if (stem != null)
        {
            if (_colorDescriptionMapper.TryGetValue($"{hex}/{color}", out value))
            {
                return value;
            }
            if (_completionUtilities.CustomColorMappers != null && _completionUtilities.CustomColorMappers.TryGetValue(stem, out dict) == false)
            {
                if (_completionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
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
                if (_completionUtilities.ColorToRgbMapper.TryGetValue(color, out var value2) && value == value2)
                {
                    if (_completionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
                    {
                        return null;
                    }
                }
            }
            else if (_completionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
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
            if (_completionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
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
        if (_completionUtilities.CustomColorMappers.ContainsKey(stem))
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
