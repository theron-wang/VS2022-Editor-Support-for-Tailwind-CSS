using EnvDTE;
using Microsoft.VisualStudio.Language.Intellisense;
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
    protected readonly ColorIconGenerator _colorIconGenerator;
    private readonly DescriptionGenerator _descriptionGenerator;
    protected readonly SettingsProvider _settingsProvider;
    protected readonly ITextBuffer _textBuffer;

    protected bool? _showAutocomplete;

    protected ClassCompletionGenerator(ITextBuffer textBuffer, CompletionUtilities completionUtils, ColorIconGenerator colorIconGenerator, DescriptionGenerator descriptionGenerator, SettingsProvider settingsProvider)
    {
        _textBuffer = textBuffer;
        _completionUtils = completionUtils;
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

        var prefix = _completionUtils.Prefix;

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

        if (string.IsNullOrWhiteSpace(currentClass) == false || isImportant)
        {
            IEnumerable<TailwindClass> scope;
            string currentClassStem = "!";
            if (isImportant && string.IsNullOrWhiteSpace(currentClass))
            {
                scope = _completionUtils.Classes;
            }
            else
            {
                string searchClass;
                if (currentClass.Length > 0 && currentClass[0] == '-')
                {
                    currentClassStem = $"-{currentClass.Split('-')[1]}";
                    searchClass = $"-{currentClass.TrimStart('-')}";
                }
                else
                {
                    currentClassStem = currentClass.Split('-')[0];
                    searchClass = currentClass;
                }

                var startsWith = currentClass[0].ToString();
                if (currentClass.Length > 1)
                {
                    startsWith += currentClass[1];
                }
                scope = _completionUtils.Classes.Where(
                    c => c.Name.Contains(searchClass) || c.Name.StartsWith(startsWith) || c.UseColors);

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

                    if (_completionUtils.CustomColorMappers != null && _completionUtils.CustomColorMappers.ContainsKey(twClass.Name))
                    {
                        colors = _completionUtils.CustomColorMappers[twClass.Name].Keys;
                    }
                    else
                    {
                        colors = _completionUtils.ColorToRgbMapper.Keys;
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

                        if (!_completionUtils.IsClassAllowed(className))
                        {
                            continue;
                        }

                        completions.Add(
                                    new Completion(className,
                                                        modifiersAsString + className,
                                                        _descriptionGenerator.GetDescription(className),
                                                        _colorIconGenerator.GetImageFromColor(twClass.Name, color, color == "transparent" ? 0 : 100),
                                                        null));

                        if (twClass.UseOpacity && currentClass.Contains(color) && currentClass.Contains('/'))
                        {
                            foreach (var opacity in _completionUtils.Opacity)
                            {
                                if (!_completionUtils.IsClassAllowed($"{className}/{opacity}"))
                                {
                                    continue;
                                }

                                completions.Add(
                                        new Completion($"{className}/{opacity}",
                                                            $"{modifiersAsString}{className}/{opacity}",
                                                            _descriptionGenerator.GetDescription($"{className}/{opacity}"),
                                                            _colorIconGenerator.GetImageFromColor(twClass.Name, color, opacity),
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

                    if (_completionUtils.CustomSpacingMappers != null && _completionUtils.CustomSpacingMappers.TryGetValue(twClass.Name, out var value))
                    {
                        spacings = value.Keys;
                    }
                    else
                    {
                        spacings = _completionUtils.SpacingMapper.Keys;
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

                        if (!_completionUtils.IsClassAllowed(className))
                        {
                            continue;
                        }

                        completions.Add(
                            new Completion(className,
                                                modifiersAsString + className,
                                                _descriptionGenerator.GetDescription(className),
                                                _completionUtils.TailwindLogo,
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
                    new Completion(className + "[]",
                                        modifiersAsString + className + "[]",
                                        twClass.Name + "[]",
                                        _completionUtils.TailwindLogo,
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

                    if (!_completionUtils.IsClassAllowed(className))
                    {
                        continue;
                    }

                    completions.Add(
                    new Completion(className,
                                        modifiersAsString + className,
                                        _descriptionGenerator.GetDescription(twClass.Name),
                                        _completionUtils.TailwindLogo,
                                        null));
                }
            }

            if (_completionUtils.PluginClasses != null)
            {
                foreach (var pluginClass in _completionUtils.PluginClasses)
                {
                    if (!_completionUtils.IsClassAllowed(pluginClass))
                    {
                        continue;
                    }

                    completions.Add(
                        new Completion(pluginClass.TrimStart('.'),
                                            modifiersAsString + prefix + pluginClass.TrimStart('.'),
                                            _descriptionGenerator.GetDescription(pluginClass.TrimStart('.')),
                                            _completionUtils.TailwindLogo,
                                            null));
                }
            }
        }
        var completionsToAddToEnd = new List<Completion>();
        foreach (var modifier in _completionUtils.Modifiers)
        {
            if (modifiers.Contains(modifier) == false)
            {
                completions.Add(
                    new Completion(modifier + ":",
                                        modifiersAsString + modifier + ":",
                                        modifier,
                                        _completionUtils.TailwindLogo,
                                        null));

                completionsToAddToEnd.Add(
                    new Completion("group-" + modifier + ":",
                                        modifiersAsString + "group-" + modifier + ":",
                                        "group-" + modifier,
                                        _completionUtils.TailwindLogo,
                                        null));

                completionsToAddToEnd.Add(
                    new Completion("peer-" + modifier + ":",
                                        modifiersAsString + "peer-" + modifier + ":",
                                        "peer-" + modifier,
                                        _completionUtils.TailwindLogo,
                                        null));
            }
        }

        if (_completionUtils.PluginModifiers != null)
        {
            foreach (var modifier in _completionUtils.PluginModifiers)
            {
                if (modifiers.Contains(modifier) == false)
                {
                    completions.Add(
                        new Completion(modifier,
                                            modifiersAsString + modifier + ":",
                                            modifier,
                                            _completionUtils.TailwindLogo,
                                            null));
                }
            }
        }

        foreach (var screen in _completionUtils.Screen)
        {
            if (modifiers.Contains(screen) == false)
            {
                completions.Add(
                    new Completion(screen + ":",
                                        modifiersAsString + screen + ":",
                                        screen,
                                        _completionUtils.TailwindLogo,
                                        null));

                completionsToAddToEnd.Add(
                    new Completion("max-" + screen + ":",
                                        modifiersAsString + "max-" + screen + ":",
                                        screen,
                                        _completionUtils.TailwindLogo,
                                        null));

            }
        }

        // keep non-peer, non-group, non-max modifiers at the end
        completions.AddRange(completionsToAddToEnd);

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
        return Task.CompletedTask;
    }
}
