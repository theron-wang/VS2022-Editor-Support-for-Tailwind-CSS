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

            // If we've already crossed over this part, don't sort again
            if (indexOfClass < lastIndex)
            {
                continue;
            }

            // Verify that we are in an HTML tag
            var closeAngleBracket = file.LastIndexOf('>', indexOfClass);
            var openAngleBracket = file.LastIndexOf('<', indexOfClass);

            if (openAngleBracket == -1 || closeAngleBracket > openAngleBracket)
            {
                continue;
            }

            yield return file.Substring(lastIndex, indexOfClass - lastIndex);

            lastIndex = match.Index + match.Length;

            if (lastIndex >= file.Length)
            {
                yield return file.Substring(indexOfClass);
                yield break;
            }

            // returns the text up to the content capture group (such as class=" or class=')
            var total = match.Value;
            var classContent = ClassRegexHelper.GetClassTextGroup(match).Value;
            yield return total.Substring(0, total.IndexOf(classContent));

            yield return SortSegment(classContent, config);
            yield return total.Substring(total.IndexOf(classContent) + classContent.Length);
        }

        yield return file.Substring(lastIndex);
    }
}
