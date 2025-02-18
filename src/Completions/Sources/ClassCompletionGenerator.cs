﻿using EnvDTE;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Sources;

internal abstract class ClassCompletionGenerator : IDisposable
{
    protected readonly CompletionUtilities _completionUtils;
    protected ProjectCompletionValues _projectCompletionValues;
    protected readonly ColorIconGenerator _colorIconGenerator;
    private readonly DescriptionGenerator _descriptionGenerator;
    protected readonly SettingsProvider _settingsProvider;
    protected readonly ITextBuffer _textBuffer;

    protected bool? _showAutocomplete;

    protected ClassCompletionGenerator(ITextBuffer textBuffer, CompletionUtilities completionUtils, ColorIconGenerator colorIconGenerator, DescriptionGenerator descriptionGenerator, SettingsProvider settingsProvider)
    {
        _textBuffer = textBuffer;
        _completionUtils = completionUtils;
        _projectCompletionValues = completionUtils.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());
        _colorIconGenerator = colorIconGenerator;
        _descriptionGenerator = descriptionGenerator;
        _settingsProvider = settingsProvider;

        _settingsProvider.OnSettingsChanged += SettingsChangedAsync;
    }

    /// <summary>
    /// Gets relevant Tailwind CSS completions for a certain input
    /// </summary>
    /// <param name="classRaw">The current class text from the caret to the previous break area (i.e. ' ')</param>
    /// <returns>A list of completions</returns>
    protected List<Completion> GetCompletions(string classRaw)
    {
        var modifiers = classRaw.Split(':').ToList();

        var currentClass = modifiers.Last();

        var isImportant = false;
        // Handle important modifier
        // Handle !px-0, but not !!px-0
        if (currentClass.StartsWith("!"))
        {
            if (currentClass.Length >= 2 && currentClass[1] == '!')
            {
                return [];
            }
            currentClass = currentClass.Substring(1);
            isImportant = true;
        }

        var prefix = _projectCompletionValues.Prefix;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "";
        }

        if (isImportant)
        {
            prefix = $"!{prefix}";
        }

        if (!string.IsNullOrWhiteSpace(currentClass) && !string.IsNullOrWhiteSpace(prefix))
        {
            if (currentClass.StartsWith(prefix))
            {
                currentClass = currentClass.Substring(prefix.Length);
            }
            else if (currentClass.StartsWith($"-{prefix}"))
            {
                currentClass = $"-{currentClass.Substring(prefix.Length + 1)}";
            }
        }

        modifiers.RemoveAt(modifiers.Count - 1);

        var completions = new List<Completion>();

        var modifiersAsString = string.Join(":", modifiers);
        if (string.IsNullOrWhiteSpace(modifiersAsString) == false)
        {
            modifiersAsString += ":";
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

                    if (className.StartsWith("-"))
                    {
                        className = $"-{prefix}{className.TrimStart('-')}";
                    }
                    else
                    {
                        className = $"{prefix}{className}";
                    }

                    if (!_projectCompletionValues.IsClassAllowed(className))
                    {
                        continue;
                    }

                    completions.Add(
                                new Completion(className,
                                                    modifiersAsString + className,
                                                    _descriptionGenerator.GetDescription(className, _projectCompletionValues),
                                                    _colorIconGenerator.GetImageFromColor(_projectCompletionValues, twClass.Name, color, color == "transparent" ? 0 : 100),
                                                    null));

                    if (twClass.UseOpacity && currentClass.Contains(color) && currentClass.Contains('/') && currentClass.StartsWith(className))
                    {
                        foreach (var opacity in _completionUtils.Opacity)
                        {
                            if (!_projectCompletionValues.IsClassAllowed($"{className}/{opacity}"))
                            {
                                continue;
                            }

                            completions.Add(
                                    new Completion($"{className}/{opacity}",
                                                        $"{modifiersAsString}{className}/{opacity}",
                                                        _descriptionGenerator.GetDescription($"{className}/{opacity}", _projectCompletionValues),
                                                        _colorIconGenerator.GetImageFromColor(_projectCompletionValues, twClass.Name, color, opacity),
                                                        null));
                        }
                        completions.Add(
                                    new Completion(className + "/[]",
                                                        modifiersAsString + className + "/[]",
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

                    if (className.StartsWith("-"))
                    {
                        className = $"-{prefix}{className.TrimStart('-')}";
                    }
                    else
                    {
                        className = $"{prefix}{className}";
                    }

                    if (!_projectCompletionValues.IsClassAllowed(className))
                    {
                        continue;
                    }

                    completions.Add(
                        new Completion(className,
                                            modifiersAsString + className,
                                            _descriptionGenerator.GetDescription(className, _projectCompletionValues),
                                            _completionUtils.TailwindLogo,
                                            null));
                }
            }
            else if (twClass.HasArbitrary)
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
                new Completion(className + "[]",
                                    modifiersAsString + className + "[]",
                                    twClass.Name + "[]",
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

                if (className.StartsWith("-"))
                {
                    className = $"-{prefix}{className.TrimStart('-')}";
                }
                else
                {
                    className = $"{prefix}{className}";
                }

                if (!_projectCompletionValues.IsClassAllowed(className))
                {
                    continue;
                }

                completions.Add(
                new Completion(className,
                                    modifiersAsString + className,
                                    _descriptionGenerator.GetDescription(twClass.Name, _projectCompletionValues),
                                    _completionUtils.TailwindLogo,
                                    null));
            }
        }

        if (_projectCompletionValues.PluginClasses != null)
        {
            foreach (var pluginClass in _projectCompletionValues.PluginClasses)
            {
                if (!_projectCompletionValues.IsClassAllowed(pluginClass))
                {
                    continue;
                }

                completions.Add(
                    new Completion(pluginClass.TrimStart('.'),
                                        modifiersAsString + prefix + pluginClass.TrimStart('.'),
                                        _descriptionGenerator.GetDescription(pluginClass.TrimStart('.'), _projectCompletionValues),
                                        _completionUtils.TailwindLogo,
                                        null));
            }
        }

        var modifierCompletions = new List<Completion>();
        var modifierCompletionsToAddToEnd = new List<Completion>();

        foreach (var modifier in _projectCompletionValues.Modifiers)
        {
            var description = _descriptionGenerator.GetModifierDescription(modifier, _projectCompletionValues);

            if (modifiers.Contains(modifier) == false)
            {
                var completion = new Completion(modifier + ":",
                    modifiersAsString + modifier + ":",
                    description,
                    _completionUtils.TailwindLogo,
                    null);

                completion.Properties.AddProperty("modifier", true);
                modifierCompletions.Add(completion);

                if (_projectCompletionValues.Version == TailwindVersion.V3 && description.StartsWith("&:") && description.Substring(2) == modifier)
                {
                    completion = new Completion("group-" + modifier + ":",
                        modifiersAsString + "group-" + modifier + ":",
                        _descriptionGenerator.GetModifierDescription("group-" + modifier, _projectCompletionValues),
                        _completionUtils.TailwindLogo,
                        null);
                    completion.Properties.AddProperty("modifier", true);
                    modifierCompletionsToAddToEnd.Add(completion);

                    completion = new Completion("peer-" + modifier + ":",
                        modifiersAsString + "peer-" + modifier + ":",
                        _descriptionGenerator.GetModifierDescription("peer-" + modifier, _projectCompletionValues),
                        _completionUtils.TailwindLogo,
                        null);
                    completion.Properties.AddProperty("modifier", true);
                    modifierCompletionsToAddToEnd.Add(completion);
                }
            }
        }

        if (_projectCompletionValues.PluginModifiers != null)
        {
            foreach (var modifier in _projectCompletionValues.PluginModifiers)
            {
                if (modifiers.Contains(modifier) == false)
                {
                    var completion = new Completion(modifier,
                                            modifiersAsString + modifier + ":",
                                            modifier,
                                            _completionUtils.TailwindLogo,
                                            null);

                    completion.Properties.AddProperty("modifier", true);
                    modifierCompletions.Add(completion);
                }
            }
        }

        foreach (var screen in _projectCompletionValues.Screen)
        {
            if (modifiers.Contains(screen) == false)
            {
                var completion = new Completion(screen + ":",
                                        modifiersAsString + screen + ":",
                                        screen,
                                        _completionUtils.TailwindLogo,
                                        null);

                completion.Properties.AddProperty("modifier", true);
                modifierCompletions.Add(completion);

                if (_projectCompletionValues.Version == TailwindVersion.V3)
                {
                    completion = new Completion("max-" + screen + ":",
                                            modifiersAsString + "max-" + screen + ":",
                                            screen,
                                            _completionUtils.TailwindLogo,
                                            null);
                    completion.Properties.AddProperty("modifier", true);
                    modifierCompletionsToAddToEnd.Add(completion);
                }
            }
        }

        // keep peer, group, max modifiers at the end
        modifierCompletions.AddRange(modifierCompletionsToAddToEnd);

        if (string.IsNullOrWhiteSpace(currentClass))
        {
            completions.InsertRange(0, modifierCompletions);
        }
        else
        {
            completions.AddRange(modifierCompletions);
        }

        foreach (var completion in completions)
        {
            completion.Properties.AddProperty("tailwind", true);
        }

        return completions;
    }

    public virtual void Dispose()
    {
        _settingsProvider.OnSettingsChanged -= SettingsChangedAsync;
    }

    private Task SettingsChangedAsync(TailwindSettings settings)
    {
        _showAutocomplete = settings.EnableTailwindCss;

        _projectCompletionValues = _completionUtils.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());

        return Task.CompletedTask;
    }
}
