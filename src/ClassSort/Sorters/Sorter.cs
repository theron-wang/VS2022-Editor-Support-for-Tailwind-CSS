using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
internal abstract class Sorter
{
    [Import]
    public CompletionUtilities CompletionUtilities { get; set; }
    [Import]
    public ClassSortUtilities ClassSortUtilities { get; set; }

    public abstract string[] Handled { get; }

    public string Sort(string input, TailwindConfiguration config)
    {
        var output = new StringBuilder();

        foreach (var segment in GetSegments(input, config))
        {
            output.Append(segment);
        }

        return output.ToString();
    }

    protected abstract IEnumerable<string> GetSegments(string input, TailwindConfiguration config);

    protected string SortSegment(string classText, TailwindConfiguration config)
    {
        var classes = classText.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

        var sorted = Sort(classes, config);

        bool shouldMoveImportant = Handled.Contains(".css") && sorted.Contains("!important");

        var newlines = classText.Select((c, i) => (c, i))
            .Where(p => p.c == '\n')
            .Select(p => p.i).ToList();

        var sortedSegment = new StringBuilder();

        int index = 0;
        int nextNewLineIndex = 0;

        foreach (var sortedClass in sorted)
        {
            if (shouldMoveImportant && sortedClass == "!important")
            {
                continue;
            }

            var before = index;

            sortedSegment.Append(sortedClass);
            index += sortedClass.Length;

            if (nextNewLineIndex < newlines.Count &&
                before <= newlines[nextNewLineIndex] && newlines[nextNewLineIndex] <= index)
            {
                sortedSegment.AppendLine();
                nextNewLineIndex++;
            }
            else
            {
                sortedSegment.Append(' ');
            }
            index++;
        }

        if (shouldMoveImportant)
        {
            sortedSegment.Append("!important");
        }

        return sortedSegment.ToString().Trim();
    }

    protected IEnumerable<string> Sort(IEnumerable<string> classes, TailwindConfiguration config)
    {
        var result = classes
            .OrderBy(className =>
            {
                var count = className.Count(c => c == ':');

                if (count > 0 && className.StartsWith("group-"))
                {
                    return 0;
                }

                return count;
            })
            .ThenBy(className =>
            {
                if (className.Contains(':'))
                {
                    var modifier = className.Split(':').First();
                    // Modifiers should be sorted after
                    if (modifier.StartsWith("group-"))
                    {
                        return 0;
                    }
                    else if (CompletionUtilities.Screen.Contains(modifier))
                    {
                        return ClassSortUtilities.ModifierOrder.Count + CompletionUtilities.Screen.IndexOf(modifier);
                    }
                    else if (ClassSortUtilities.ModifierOrder.TryGetValue(modifier, out int index))
                    {
                        return index;
                    }
                    return int.MaxValue;
                }
                return 0;
            })
            .ThenBy(className =>
            {
                if (ImportantModiferHelper.IsImportantModifier(className))
                {
                    className = className.TrimStart('!');
                }

                var classToSearch = className;

                if (string.IsNullOrWhiteSpace(config?.Prefix) == false)
                {
                    classToSearch = classToSearch
                        .TrimPrefix(config?.Prefix, StringComparison.InvariantCultureIgnoreCase)
                        .TrimPrefix("-" + config?.Prefix, StringComparison.InvariantCultureIgnoreCase);

                    if (className.StartsWith("-"))
                    {
                        classToSearch = $"-{classToSearch}";
                    }

                    // Prefix not used, don't sort
                    if (classToSearch == className)
                    {
                        return -1;
                    }
                }

                if (classToSearch.Contains('-'))
                {
                    var ending = classToSearch.Split('-').Last();
                    var stem = classToSearch.Substring(0, classToSearch.LastIndexOf('-'));

                    if (CompletionUtilities.SpacingMapper.ContainsKey(ending) ||
                        (
                            ending.Length > 2 &&
                            ending[0] == '[' &&
                            ending[ending.Length - 1] == ']' &&
                            ClassSortUtilities.ClassOrder.ContainsKey($"{stem}-{{s}}")
                        ) ||
                        CompletionUtilities.CustomSpacingMappers.ContainsKey($"{stem}-{{0}}"))
                    {
                        classToSearch = $"{stem}-{{s}}";
                    }
                    else if (CompletionUtilities.ColorToRgbMapper.ContainsKey(ending) ||
                            (
                                ending.Length > 2 &&
                                ending[0] == '[' &&
                                ending[ending.Length - 1] == ']' &&
                                ClassSortUtilities.ClassOrder.ContainsKey($"{stem}-{{c}}")
                            ) ||
                            CompletionUtilities.CustomColorMappers.ContainsKey($"{stem}-{{0}}"))
                    {
                        classToSearch = $"{stem}-{{c}}";
                    }
                    else if (ending.Length > 2 && ending[0] == '[' && ending[ending.Length - 1] == ']')
                    {
                        // We'll approximate the class here. For example, if we do rounded-[1.2rem],
                        // find rounded instead
                        classToSearch = stem;
                    }
                    else
                    {
                        if (stem.Contains('-'))
                        {
                            var splitIndex = classToSearch.LastIndexOf('-', classToSearch.LastIndexOf('-') - 1);
                            stem = classToSearch.Substring(0, splitIndex);
                            ending = classToSearch.Substring(splitIndex + 1);

                            if (CompletionUtilities.ColorToRgbMapper.ContainsKey(ending) ||
                                CompletionUtilities.CustomColorMappers.ContainsKey($"{stem}-{{0}}"))
                            {
                                classToSearch = $"{stem}-{{c}}";
                            }
                        }
                    }
                }

                if (!CompletionUtilities.IsClassAllowed(className))
                {
                    return -1;
                }

                return ClassSortUtilities.ClassOrder.TryGetValue(classToSearch, out int index) ? index : -1;
            });

        return result;
    }

    protected (int index, char terminator) GetNextIndexOfClass(string file, int startIndex, string searchFor = "class=")
    {
        var single = file.IndexOf($"{searchFor}'", startIndex, StringComparison.InvariantCultureIgnoreCase);
        var doubleQuote = file.IndexOf($"{searchFor}\"", startIndex, StringComparison.InvariantCultureIgnoreCase);

        if (single == -1 && doubleQuote == -1)
        {
            return (-1, ' ');
        }
        else if (single == -1)
        {
            return (doubleQuote, '"');
        }
        else if (doubleQuote == -1)
        {
            return (single, '\'');
        }
        else if (single < doubleQuote)
        {
            return (single, '\'');
        }
        else
        {
            return (doubleQuote, '"');
        }
    }
}
