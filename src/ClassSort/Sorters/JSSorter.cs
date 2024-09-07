using System.Collections.Generic;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
[Export(typeof(Sorter))]
internal class JSSorter : Sorter
{
    public override string[] Handled { get; } = [".jsx", ".tsx"];

    protected override IEnumerable<string> GetSegments(string file, TailwindConfiguration config)
    {
        (int indexOfClass, char terminator) = GetNextIndexOfClass(file, 0, " className=");

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

            // Handle edge cases: Alpine JS, Vue, etc.
            // <div x-bind:class="! open ? 'hidden' : ''">
            var classText = file.Substring(indexOfClass + 7, lastIndex - (indexOfClass + 7));

            bool inside = false;
            int index = 0;

            char lookFor = file[indexOfClass + 6] == '"' ? '\'' : '"';

            var from = 0;

            while (index != -1)
            {
                index = classText.IndexOf(lookFor, index + 1);
                if (index == -1)
                {
                    if (from == 0)
                    {
                        yield return SortSegment(classText, config);
                    }
                    else if (inside)
                    {
                        yield return lookFor + SortSegment(classText.Substring(from + 1), config);
                    }
                    else
                    {
                        yield return classText.Substring(from);
                    }

                    break;
                }

                if (index == 0 || classText[index - 1] != '\\')
                {
                    if (inside)
                    {
                        yield return lookFor + SortSegment(classText.Substring(from + 1, index - from - 1), config);
                        inside = false;
                    }
                    else
                    {
                        yield return classText.Substring(from, index - from);
                        inside = true;
                    }
                    from = index;
                }
            }

            (indexOfClass, terminator) = GetNextIndexOfClass(file, indexOfClass + 1);
        }

        yield return file.Substring(lastIndex);
    }
}
