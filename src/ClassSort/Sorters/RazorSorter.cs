using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
[Export(typeof(Sorter))]
internal class RazorSorter : Sorter
{
    public override string[] Handled { get; } = [".razor", ".cshtml"];

    protected override IEnumerable<string> GetSegments(string file, TailwindConfiguration config)
    {
        int lastIndex = 0;
        int indexOfClass;

        foreach (var match in ClassRegexHelper.GetClassesRazorEnumerator(file))
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

            var tokens = new List<string>();
            var razorIndices = new HashSet<int>();
            
            // A list of numbers that represent where a new line should be inserted;
            // each number represents a text token in tokens
            var newLines = new HashSet<int>();

            foreach ((var token, var index) in ClassRegexHelper.SplitRazorClasses(classContent).Select((m, i) => (m, i)))
            {
                tokens.Add(token.Value);
                if (token.Value.StartsWith("@"))
                {
                    razorIndices.Add(index);
                }
                if ((token.Index + token.Length < match.Value.Length && match.Value[token.Index + token.Length] == '\n') ||
                    // first case handles \n, second case handles \r\n
                    token.Index + token.Length + 1 < match.Value.Length && match.Value[token.Index + token.Length + 1] == '\n')
                {
                    // This means that after this current token, there should be a newline
                    newLines.Add(index);
                }
            }

            var sorted = SortRazorSegment(tokens, razorIndices, config);

            var text = new StringBuilder();

            foreach ((var i, var token) in sorted.Select((v, i) => (i, v)))
            {
                text.Append(token);
                if (newLines.Contains(i))
                {
                    text.AppendLine();
                }
                else
                {
                    text.Append(' ');
                }
            }

            yield return text.ToString().Trim();
            yield return total.Substring(total.IndexOf(classContent) + classContent.Length);
        }

        yield return file.Substring(lastIndex);
    }
    private IEnumerable<string> SortRazorSegment(List<string> classes, HashSet<int> razorIndices, TailwindConfiguration config)
    {
        var sorted = Sort(classes
            .Select((v, i) => (v, i))
            .Where(v => !razorIndices.Contains(v.i))
            .Select(v => v.v), config);

        int lastIndex = -1;
        int start = 0;

        foreach (int index in razorIndices)
        {
            int between = index - lastIndex - 1;

            if (between < 0)
            {
                between = 0;
            }

            foreach (var s in sorted.Skip(start).Take(between))
            {
                yield return s;
            }
            yield return classes[index];

            lastIndex = index;
            start += between;
        }

        foreach (var s in sorted.Skip(start))
        {
            yield return s;
        }
    }

    private class Token(string text, bool isInRazor)
    {
        public string Text { get; set; } = text;
        public bool IsInRazor { get; set; } = isInRazor;
    }
}
