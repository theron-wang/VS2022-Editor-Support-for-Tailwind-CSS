using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
[Export(typeof(Sorter))]
internal class HtmlSorter : Sorter
{
    public override string[] Handled { get; } = [".html", ".aspx", ".ascx"];

    protected override IEnumerable<string> GetSegments(string file, TailwindConfiguration config)
    {
        int lastIndex = 0;
        int indexOfClass;

        foreach (var match in ClassRegexHelper.GetClassesNormalEnumerator(file))
        {
            indexOfClass = match.Index;

            // Verify that we are in an HTML tag
            var closeAngleBracket = file.LastIndexOf('>', indexOfClass);
            var openAngleBracket = file.LastIndexOf('<', indexOfClass);

            if (openAngleBracket == -1 || closeAngleBracket > openAngleBracket)
            {
                continue;
            }

            yield return file.Substring(lastIndex, indexOfClass - lastIndex);

            lastIndex = match.Index + match.Length - 1;

            if (lastIndex >= file.Length)
            {
                yield return file.Substring(indexOfClass);
                yield break;
            }

            // return class=" or class='
            // match.Groups[0] is the whole class: class="..."
            // match.Groups[1] is the quote type: " or '
            var total = match.Groups[0].Value;
            yield return total.Substring(0, total.IndexOf(match.Groups[1].Value) + match.Groups[1].Length);

            var classContent = ClassRegexHelper.GetClassTextGroup(match).Value;
            if (classContent.Contains('\'') || classContent.Contains('\"'))
            {
                // TODO: handle special cases like Alpine JS, Angular, etc.
                // <div x-bind:class="open ? '' : 'hidden'">
                // A potential solution is to use another regex to split based on quotation pairs, and 
                // sort each pair.
                // At the moment, we will just leave these cases unsorted.
                yield return classContent;
            }
            else
            {
                yield return SortSegment(classContent, config);
            }
        }

        yield return file.Substring(lastIndex);
    }
}
