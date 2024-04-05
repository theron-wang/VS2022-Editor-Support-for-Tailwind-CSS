using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
[Export(typeof(Sorter))]
internal class HtmlSorter : Sorter
{
    public override string[] Handled { get; } = [".html", ".aspx", ".ascx"];

    protected override IEnumerable<string> GetSegments(string file, TailwindConfiguration config)
    {
        (int indexOfClass, char terminator) = GetNextIndexOfClass(file, 0);

        int lastIndex = 0;

        while (indexOfClass != -1)
        {
            // Verify that we are in an HTML tag
            var closeAngleBracket = file.LastIndexOf('>', indexOfClass);
            var openAngleBracket = file.LastIndexOf('<', indexOfClass);

            if (openAngleBracket == -1 || closeAngleBracket > openAngleBracket)
            {
                (indexOfClass, terminator) = GetNextIndexOfClass(file, indexOfClass + 1);
                continue;
            }

            yield return file.Substring(lastIndex, indexOfClass - lastIndex);

            lastIndex = file.IndexOf(terminator, file.IndexOf(terminator, indexOfClass) + 1);

            if (lastIndex == -1)
            {
                yield return file.Substring(indexOfClass);
                yield break;
            }

            // return class=" or class='
            yield return file.Substring(indexOfClass, 7);
            yield return SortSegment(file.Substring(indexOfClass + 7, lastIndex - (indexOfClass + 7)).Split(new char[0], StringSplitOptions.RemoveEmptyEntries), config);
            (indexOfClass, terminator) = GetNextIndexOfClass(file, indexOfClass + 1);
        }

        yield return file.Substring(lastIndex);
    }
}
