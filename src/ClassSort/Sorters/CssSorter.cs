using System.Collections.Generic;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
[Export(typeof(Sorter))]
internal class CssSorter : Sorter
{
    public override string[] Handled { get; } = [".css", ".tcss"];

    protected override IEnumerable<string> GetSegments(string file, TailwindConfiguration config)
    {
        int indexOfApply = file.IndexOf("@apply");

        int lastIndex = 0;

        while (indexOfApply != -1)
        {
            // Verify that we are in the right context
            // Check to see if it is whitespace between here and { or ;, whichever comes first
            var context = file.LastIndexOfAny([';', '{'], indexOfApply) + 1;

            if (context == -1 || !string.IsNullOrWhiteSpace(file.Substring(context, indexOfApply - context)))
            {
                indexOfApply = file.IndexOf("@apply", indexOfApply + 1);
                continue;
            }

            yield return file.Substring(lastIndex, indexOfApply - lastIndex);

            lastIndex = file.IndexOfAny([';', '}'], indexOfApply);

            if (lastIndex == -1)
            {
                yield return file.Substring(indexOfApply);
                yield break;
            }

            // return @apply
            yield return "@apply ";
            yield return SortSegment(file.Substring(indexOfApply + 6, lastIndex - (indexOfApply + 6)), config);
            indexOfApply = file.IndexOf("@apply", indexOfApply + 1);
        }

        yield return file.Substring(lastIndex);
    }
}
