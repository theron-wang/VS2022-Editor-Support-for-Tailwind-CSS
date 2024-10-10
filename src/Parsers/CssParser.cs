using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;

namespace TailwindCSSIntellisense.Parsers;
internal static class CssParser
{
    public static IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
    {
        char[] endings = [';', '{', '}'];

        var text = span.GetText();
        var last = text.LastIndexOfAny(endings);

        // The goal of this method is to split a larger SnapshotSpan into smaller SnapshotSpans
        // Each smaller SnapshotSpan will be a segment between ;, {, }, and snapshot boundaries

        if (span.End != snapshot.Length && (text is null || string.IsNullOrWhiteSpace(text.Trim(endings)) || last == -1 || string.IsNullOrWhiteSpace(text.Substring(last + 1)) == false))
        {
            SnapshotPoint end = span.End;
            bool hitNonEnding = false;
            while (end < snapshot.Length - 1 && (endings.Contains(end.GetChar()) == false || !hitNonEnding))
            {
                if (endings.Contains(end.GetChar()) == false)
                {
                    hitNonEnding = true;
                }
                end += 1;
            }

            if (string.IsNullOrWhiteSpace(text) == false && hitNonEnding)
            {
                while (endings.Contains(end.GetChar()))
                {
                    end -= 1;

                    if (end < span.Start || end == 0)
                    {
                        yield break;
                    }
                }

                if (end < snapshot.Length - 1)
                {
                    // SnapshotPoint end is exclusive
                    end += 1;
                }
            }

            span = new SnapshotSpan(span.Start, end);
        }

        var first = text.IndexOfAny(endings);

        if (text is null || string.IsNullOrWhiteSpace(text.Trim(endings)) || first == -1 || string.IsNullOrWhiteSpace(text.Substring(0, first)) == false)
        {
            SnapshotPoint start = span.Start;

            if (span.End == start)
            {
                if (start == 0)
                {
                    yield break;
                }
                start -= 1;
            }

            bool hitNonEnding = false;
            while (start > 0 && (endings.Contains(start.GetChar()) == false || !hitNonEnding))
            {
                if (endings.Contains(start.GetChar()) == false)
                {
                    hitNonEnding = true;
                }
                start -= 1;
            }

            while (endings.Contains(start.GetChar()))
            {
                start += 1;

                if (start >= span.End)
                {
                    yield break;
                }
            }

            span = new SnapshotSpan(start, span.End);
        }

        var segmentStart = span.Start;
        var segmentEnd = span.Start;

        while (segmentEnd < span.End && segmentEnd < snapshot.Length)
        {
            segmentEnd += 1;

            if (segmentEnd == snapshot.Length)
            {
                yield return new SnapshotSpan(segmentStart, segmentEnd);
                yield break;
            }

            if (endings.Contains(segmentEnd.GetChar()))
            {
                yield return new SnapshotSpan(segmentStart, segmentEnd);

                segmentStart = segmentEnd + 1;
            }
        }
    }
}
