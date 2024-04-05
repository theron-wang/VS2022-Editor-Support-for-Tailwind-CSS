using System.Collections.Generic;
using System;
using TailwindCSSIntellisense.Configuration;
using System.Linq;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using Microsoft.VisualStudio.Shell;
using System.Text;

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

    protected string SortSegment(IEnumerable<string> classes, TailwindConfiguration config)
    {
        var result = classes.OrderBy(className => className.Count(c => c == ':'))
            .ThenBy(className =>
            {
                if (className.Contains(':'))
                {
                    var modifier = className.Split(':').First();
                    if (ClassSortUtilities.ModifierOrder.TryGetValue(modifier, out int index) == false)
                    {
                        return int.MaxValue;
                    }
                    return index;
                }
                return 0;
            })
            .ThenBy(className =>
            {
                var classToSearch = className;

                if (string.IsNullOrWhiteSpace(config?.Prefix) == false)
                {
                    classToSearch = classToSearch
                        .TrimPrefix(config?.Prefix + "-", StringComparison.InvariantCultureIgnoreCase)
                        .TrimPrefix("-" + config?.Prefix + "-", StringComparison.InvariantCultureIgnoreCase);

                    if (className.StartsWith("-"))
                    {
                        classToSearch = $"-{classToSearch}";
                    }
                }

                if (classToSearch.Contains('-'))
                {
                    var ending = classToSearch.Split('-').Last();
                    var stem = classToSearch.Substring(0, classToSearch.LastIndexOf('-'));

                    if (CompletionUtilities.SpacingMapper.ContainsKey(ending) ||
                        CompletionUtilities.CustomSpacingMappers.ContainsKey($"{stem}-{{0}}"))
                    {
                        classToSearch = $"{stem}-{{s}}";
                    }
                    else if (CompletionUtilities.ColorToRgbMapper.ContainsKey(ending) ||
                            CompletionUtilities.CustomColorMappers.ContainsKey($"{stem}-{{0}}"))
                    {
                        classToSearch = $"{stem}-{{c}}";
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

                return ClassSortUtilities.ClassOrder.TryGetValue(classToSearch, out int index) ? index : -1;
            });

        return string.Join(" ", result);
    }

    protected (int index, char terminator) GetNextIndexOfClass(string file, int startIndex)
    {
        var single = file.IndexOf("class='", startIndex, StringComparison.InvariantCultureIgnoreCase);
        var doubleQuote = file.IndexOf("class=\"", startIndex, StringComparison.InvariantCultureIgnoreCase);

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
