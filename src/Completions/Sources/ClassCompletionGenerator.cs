﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Sources;

internal abstract class ClassCompletionGenerator : IDisposable
{
    protected readonly ProjectConfigurationManager _completionUtils;
    protected ProjectCompletionValues _projectCompletionValues;
    protected readonly ColorIconGenerator _colorIconGenerator;
    protected readonly DescriptionGenerator _descriptionGenerator;
    protected readonly SettingsProvider _settingsProvider;
    private readonly CompletionConfiguration _completionConfiguration;
    protected readonly ITextBuffer _textBuffer;

    protected bool? _showAutocomplete;

    protected ClassCompletionGenerator(ITextBuffer textBuffer, ProjectConfigurationManager completionUtils, ColorIconGenerator colorIconGenerator, DescriptionGenerator descriptionGenerator, SettingsProvider settingsProvider, CompletionConfiguration completionConfiguration)
    {
        _textBuffer = textBuffer;
        _completionUtils = completionUtils;
        _projectCompletionValues = completionUtils.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());
        _colorIconGenerator = colorIconGenerator;
        _descriptionGenerator = descriptionGenerator;
        _settingsProvider = settingsProvider;
        _completionConfiguration = completionConfiguration;
        _settingsProvider.OnSettingsChanged += SettingsChangedAsync;
        _completionConfiguration.ConfigurationUpdated += ConfigurationChanged;
    }

    /// <summary>
    /// Gets relevant Tailwind CSS completions for a certain input
    /// </summary>
    /// <param name="classRaw">The current class text from the caret to the previous break area (i.e. ' ')</param>
    /// <returns>A list of completions</returns>
    protected List<Completion> GetCompletions(string classRaw, bool isRazor = false)
    {
        var prefix = _projectCompletionValues.Prefix;

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            if (!string.IsNullOrWhiteSpace(classRaw) && !classRaw.StartsWith(prefix) && _projectCompletionValues.Version >= TailwindVersion.V4)
            {
                return [];
            }
        }
        else
        {
            prefix = "";
        }

        var variants = classRaw.Split(':').ToList();

        var currentClass = variants.Last();

        var isImportant = ImportantModifierHelper.IsImportantModifier(classRaw);

        var suffix = "";

        if (isImportant)
        {
            currentClass = currentClass.Trim('!');

            if (_projectCompletionValues.Version >= TailwindVersion.V4)
            {
                suffix = "!";
            }
        }
        else if (currentClass.StartsWith("!") || currentClass.EndsWith("!"))
        {
            return [];
        }

        if (_projectCompletionValues.Version == TailwindVersion.V3)
        {
            if (isImportant)
            {
                prefix = $"!{prefix}";
            }

            if (!string.IsNullOrWhiteSpace(currentClass) && !string.IsNullOrWhiteSpace(prefix))
            {
                if (currentClass.StartsWith(prefix!))
                {
                    currentClass = currentClass.Substring(prefix!.Length);
                }
                else if (currentClass.StartsWith($"-{prefix}"))
                {
                    currentClass = $"-{currentClass.Substring(prefix!.Length + 1)}";
                }
            }
        }

        variants.RemoveAt(variants.Count - 1);

        var completions = new List<Completion>();

        // If this is in v4+, also contains the prefix
        var variantsAsString = string.Join(":", variants);
        if (string.IsNullOrWhiteSpace(variantsAsString) == false)
        {
            variantsAsString += ":";
        }

        // For use to check if it matches part of the blocklist
        var variantsAsStringUnescaped = variantsAsString;
        if (isRazor)
        {
            variantsAsStringUnescaped = variantsAsString.Replace("@@", "@").Replace("@(\"@\")", "@");
        }

        var segments = currentClass.Split('-');

        IEnumerable<TailwindClass> scope = _projectCompletionValues.Classes;
        string currentClassStem = "!";
        if (string.IsNullOrWhiteSpace(currentClass))
        {
            if (!isImportant)
            {
                currentClassStem = "";
            }
        }
        else
        {
            if (currentClass.Length > 0 && currentClass[0] == '-')
            {
                currentClassStem = $"-{currentClass.Split('-')[1]}";
            }
            else
            {
                currentClassStem = currentClass.Split('-')[0];
            }
        }

        if (currentClass.StartsWith("-") == false)
        {
            scope = scope.OrderBy(c => c.Name.StartsWith("-"));
        }

        foreach (var twClass in scope)
        {
            if (twClass.UseColors)
            {
                IEnumerable<string> colors;

                if (_projectCompletionValues.CustomColorMappers != null && _projectCompletionValues.CustomColorMappers.ContainsKey(twClass.Name))
                {
                    colors = _projectCompletionValues.CustomColorMappers[twClass.Name].Keys;
                }
                else
                {
                    colors = _projectCompletionValues.ColorMapper.Keys;
                }

                foreach (var color in colors)
                {
                    var className = string.Format(twClass.Name, color);
                    var classNameRaw = className;

                    if (_projectCompletionValues.Version == TailwindVersion.V3 && !string.IsNullOrWhiteSpace(prefix))
                    {
                        if (className.StartsWith("-"))
                        {
                            className = $"-{prefix}{className.TrimStart('-')}";
                        }
                        else
                        {
                            className = $"{prefix}{className}";
                        }
                    }

                    if (!_projectCompletionValues.IsClassAllowed(variantsAsStringUnescaped + className + suffix))
                    {
                        continue;
                    }

                    var insert = variantsAsString + (isRazor ? className.Replace("@", "@@") : className) + suffix;

                    completions.Add(
                                new Completion(className + suffix,
                                                    insert,
                                                    classNameRaw,
                                                    _colorIconGenerator.GetImageFromColor(_projectCompletionValues, twClass.Name, color, color == "transparent" ? 0 : 100),
                                                    null));

                    if (twClass.UseOpacity && currentClass.Contains(color) && currentClass.Contains('/') && currentClass.StartsWith(className))
                    {
                        foreach (var opacity in _completionUtils.Opacity)
                        {
                            if (!_projectCompletionValues.IsClassAllowed($"{variantsAsStringUnescaped}{className}/{opacity}{suffix}"))
                            {
                                continue;
                            }

                            insert = $"{variantsAsString}{(isRazor ? className.Replace("@", "@@") : className)}/{opacity}{suffix}";

                            completions.Add(
                                    new Completion($"{className}/{opacity}{suffix}",
                                                        insert,
                                                        $"{className}/{opacity}",
                                                        _colorIconGenerator.GetImageFromColor(_projectCompletionValues, twClass.Name, color, opacity),
                                                        null));
                        }

                        insert = variantsAsString + (isRazor ? className.Replace("@", "@@") : className) + "/[]" + suffix;
                        completions.Add(
                                    new Completion(className + "/[]" + suffix,
                                                        insert,
                                                        className + "/[]",
                                                        _completionUtils.TailwindLogo,
                                                        null));
                    }


                }
            }
            else if (twClass.UseSpacing)
            {
                IEnumerable<string> spacings;

                if (_projectCompletionValues.CustomSpacingMappers != null && _projectCompletionValues.CustomSpacingMappers.TryGetValue(twClass.Name, out var value))
                {
                    spacings = value.Keys;
                }
                else
                {
                    spacings = _projectCompletionValues.SpacingMapper.Keys;
                }

                foreach (var spacing in spacings)
                {
                    var className = string.IsNullOrWhiteSpace(spacing) ? twClass.Name.Replace("-{0}", "") : string.Format(twClass.Name, spacing);
                    var classNameRaw = className;

                    if (_projectCompletionValues.Version == TailwindVersion.V3 && !string.IsNullOrWhiteSpace(prefix))
                    {
                        if (className.StartsWith("-"))
                        {
                            className = $"-{prefix}{className.TrimStart('-')}";
                        }
                        else
                        {
                            className = $"{prefix}{className}";
                        }
                    }

                    if (!_projectCompletionValues.IsClassAllowed(variantsAsStringUnescaped + className + suffix))
                    {
                        continue;
                    }

                    var insert = variantsAsString + (isRazor ? className.Replace("@", "@@") : className) + suffix;
                    completions.Add(
                        new Completion(className + suffix,
                                            insert,
                                            classNameRaw,
                                            _completionUtils.TailwindLogo,
                                            null));
                }
            }
            else if (twClass.HasArbitrary)
            {
                var className = twClass.Name;
                var classNameRaw = className;

                if (_projectCompletionValues.Version == TailwindVersion.V3 && !string.IsNullOrWhiteSpace(prefix))
                {
                    if (className.StartsWith("-"))
                    {
                        className = $"-{prefix}{className.TrimStart('-')}";
                    }
                    else
                    {
                        className = $"{prefix}{className}";
                    }
                }

                if (!_projectCompletionValues.IsClassAllowed(variantsAsStringUnescaped + className + suffix))
                {
                    continue;
                }

                var insert = variantsAsString + (isRazor ? className.Replace("@", "@@") : className) + "[]" + suffix;
                completions.Add(
                new Completion(className + "[]" + suffix,
                                    insert,
                                    classNameRaw + "[]",
                                    _completionUtils.TailwindLogo,
                                    null));
            }
            else
            {
                var className = twClass.Name;

                if (className.Contains("{0}"))
                {
                    continue;
                }

                var classNameRaw = className;

                if (_projectCompletionValues.Version == TailwindVersion.V3 && !string.IsNullOrWhiteSpace(prefix))
                {
                    if (className.StartsWith("-"))
                    {
                        className = $"-{prefix}{className.TrimStart('-')}";
                    }
                    else
                    {
                        className = $"{prefix}{className}";
                    }
                }

                if (!_projectCompletionValues.IsClassAllowed(variantsAsStringUnescaped + className + suffix))
                {
                    continue;
                }

                var insert = variantsAsString + (isRazor ? className.Replace("@", "@@") : className) + suffix;
                completions.Add(
                new Completion(className + suffix,
                                    insert,
                                    classNameRaw,
                                    _completionUtils.TailwindLogo,
                                    null));

                if (currentClass.Contains('/'))
                {
                    if (classNameRaw.StartsWith("bg-linear") || classNameRaw.StartsWith("bg-radial") || classNameRaw.StartsWith("bg-conic"))
                    {
                        foreach (var modifier in KnownModifiers.GradientModifierToDescription.Keys)
                        {
                            if (!_projectCompletionValues.IsClassAllowed($"{variantsAsStringUnescaped}/{className}/{modifier}{suffix}"))
                            {
                                continue;
                            }

                            insert = $"{variantsAsString}{(isRazor ? className.Replace("@", "@@") : className)}/{modifier}{suffix}";

                            completions.Add(
                                    new Completion($"{className}/{modifier}{suffix}",
                                                        insert,
                                                        $"{classNameRaw}/{modifier}",
                                                        _completionUtils.TailwindLogo,
                                                        null));
                        }

                        insert = variantsAsString + (isRazor ? className.Replace("@", "@@") : className) + "/[]" + suffix;
                        completions.Add(
                                    new Completion(className + "/[]" + suffix,
                                                       insert,
                                                       classNameRaw + "/[]",
                                                       _completionUtils.TailwindLogo,
                                                       null));
                    }
                    else if (KnownModifiers.IsEligibleForLineHeightModifier(className, _projectCompletionValues))
                    {
                        var lineHeightModifiers = _projectCompletionValues.CssVariables.Where(v => v.Key.StartsWith("--leading-")).Select(v => v.Key.Replace("--leading-", ""));
                        foreach (var modifier in lineHeightModifiers)
                        {
                            if (!_projectCompletionValues.IsClassAllowed($"{variantsAsStringUnescaped}/{className}/{modifier}{suffix}"))
                            {
                                continue;
                            }

                            insert = $"{variantsAsString}{(isRazor ? className.Replace("@", "@@") : className)}/{modifier}{suffix}";
                            completions.Add(
                                    new Completion($"{className}/{modifier}{suffix}",
                                                        insert,
                                                        $"{classNameRaw}/{modifier}",
                                                        _completionUtils.TailwindLogo,
                                                        null));
                        }

                        insert = variantsAsString + (isRazor ? className.Replace("@", "@@") : className) + "/[]" + suffix;
                        completions.Add(
                                    new Completion(className + "/[]" + suffix,
                                                        insert,
                                                        classNameRaw + "/[]",
                                                        _completionUtils.TailwindLogo,
                                                        null));
                    }
                }
            }
        }

        if (_projectCompletionValues.PluginClasses != null)
        {
            foreach (var pluginClass in _projectCompletionValues.PluginClasses)
            {
                if (!_projectCompletionValues.IsClassAllowed(variantsAsStringUnescaped + pluginClass + suffix))
                {
                    continue;
                }

                var insert = variantsAsString + prefix + (isRazor ? pluginClass.Replace("@", "@@") : pluginClass).TrimStart('.') + suffix;

                completions.Add(
                    new Completion(pluginClass.TrimStart('.'),
                                        insert,
                                        pluginClass.TrimStart('.'),
                                        _completionUtils.TailwindLogo,
                                        null));
            }
        }

        var variantCompletions = new List<Completion>();
        var variantCompletionsToAddToEnd = new List<Completion>();

        var allVariants = _projectCompletionValues.Variants
            .SelectMany(v =>
            {
                if (v.Contains("{b}"))
                {
                    return _projectCompletionValues.Breakpoints.Keys.Select(b => $"{v.Replace("{b}", b)}");
                }
                else if (v.Contains("{c}"))
                {
                    return _projectCompletionValues.Containers.Keys.Select(c => $"{v.Replace("{c}", c)}");
                }
                else if (v.Contains("{a}"))
                {
                    return [v.Replace("{a}", "[]")];
                }
                return [v];
            });

        foreach (var variant in allVariants)
        {
            var description = _descriptionGenerator.GetVariantDescription(variant, _projectCompletionValues);

            if (variants.Contains(variant) == false)
            {
                var insert = variantsAsString + (isRazor ? variant.Replace("@", "@@") : variant) + ":";

                var completion = new Completion(variant + ":",
                    insert,
                    description,
                    _completionUtils.TailwindLogo,
                    null);

                completion.Properties.AddProperty("variant", true);
                variantCompletions.Add(completion);

                if (_projectCompletionValues.Version == TailwindVersion.V3 && description is not null && description.StartsWith("&:") && description.Substring(2) == variant)
                {
                    insert = variantsAsString + "group-" + (isRazor ? variant.Replace("@", "@@") : variant) + ":";

                    completion = new Completion("group-" + variant + ":",
                        insert,
                        _descriptionGenerator.GetVariantDescription("group-" + variant, _projectCompletionValues),
                        _completionUtils.TailwindLogo,
                        null);
                    completion.Properties.AddProperty("variant", true);
                    variantCompletionsToAddToEnd.Add(completion);

                    insert = variantsAsString + "peer-" + (isRazor ? variant.Replace("@", "@@") : variant) + ":";
                    completion = new Completion("peer-" + variant + ":",
                        insert,
                        _descriptionGenerator.GetVariantDescription("peer-" + variant, _projectCompletionValues),
                        _completionUtils.TailwindLogo,
                        null);
                    completion.Properties.AddProperty("variant", true);
                    variantCompletionsToAddToEnd.Add(completion);
                }
            }
        }

        if (_projectCompletionValues.PluginVariants != null)
        {
            foreach (var variant in _projectCompletionValues.PluginVariants)
            {
                if (variants.Contains(variant) == false)
                {
                    var insert = variantsAsString + (isRazor ? variant.Replace("@", "@@") : variant) + ":";
                    var completion = new Completion(variant + ":",
                                            insert,
                                            _projectCompletionValues.Version >= TailwindVersion.V4 ?
                                            _descriptionGenerator.GetVariantDescription(variant, _projectCompletionValues) : variant,
                                            _completionUtils.TailwindLogo,
                                            null);

                    completion.Properties.AddProperty("variant", true);
                    variantCompletions.Add(completion);
                }
            }
        }

        if (_projectCompletionValues.Version == TailwindVersion.V3)
        {
            foreach (var screen in _projectCompletionValues.Breakpoints.Keys)
            {
                if (variants.Contains(screen) == false)
                {
                    var insert = variantsAsString + (isRazor ? screen.Replace("@", "@@") : screen) + ":";

                    var completion = new Completion(screen + ":",
                                            insert,
                                            screen,
                                            _completionUtils.TailwindLogo,
                                            null);

                    completion.Properties.AddProperty("variant", true);
                    variantCompletions.Add(completion);

                    if (_projectCompletionValues.Version == TailwindVersion.V3)
                    {
                        insert = variantsAsString + "max-" + (isRazor ? screen.Replace("@", "@@") : screen) + ":";

                        completion = new Completion("max-" + screen + ":",
                                                insert,
                                                screen,
                                                _completionUtils.TailwindLogo,
                                                null);
                        completion.Properties.AddProperty("variant", true);
                        variantCompletionsToAddToEnd.Add(completion);
                    }
                }
            }
        }

        // keep peer, group, max variants at the end
        variantCompletions.AddRange(variantCompletionsToAddToEnd);

        if (string.IsNullOrWhiteSpace(currentClass))
        {
            completions.InsertRange(0, variantCompletions);
        }
        else
        {
            completions.AddRange(variantCompletions);
        }

        foreach (var completion in completions)
        {
            completion.Properties.AddProperty("tailwind", _projectCompletionValues);
        }
        return completions;
    }

    public virtual void Dispose()
    {
        _settingsProvider.OnSettingsChanged -= SettingsChangedAsync;
        _completionConfiguration.ConfigurationUpdated -= ConfigurationChanged;
    }

    private Task SettingsChangedAsync(TailwindSettings settings)
    {
        _showAutocomplete = settings.EnableTailwindCss;

        _projectCompletionValues = _completionUtils.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());

        return Task.CompletedTask;
    }

    private void ConfigurationChanged()
    {
        _projectCompletionValues = _completionUtils.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());
    }
}
