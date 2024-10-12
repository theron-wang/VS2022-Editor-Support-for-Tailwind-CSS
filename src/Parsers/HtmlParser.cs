using EnvDTE80;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Documents;
using System.Windows.Shapes;

namespace TailwindCSSIntellisense.Parsers;
internal static class HtmlParser
{
    public static IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
    {
        return GetScopesImpl(span, snapshot, "class=");
    }
    internal static IEnumerable<SnapshotSpan> GetScopesImpl(SnapshotSpan span, ITextSnapshot snapshot, string search)
    {
        char[] endings = ['"', '\''];

        var text = span.GetText();
        var last = text.LastIndexOfAny(endings);

        // The goal of this method is to split a larger SnapshotSpan into smaller SnapshotSpans
        // Each smaller SnapshotSpan will be a segment between class=" and ", or class=' and ', and snapshot boundaries

        if (span.End != snapshot.Length && (string.IsNullOrWhiteSpace(text) || last == -1 || string.IsNullOrWhiteSpace(text.Substring(last + 1)) == false))
        {
            SnapshotPoint end = span.End;
            while (end < snapshot.Length - 1 && endings.Contains(end.GetChar()) == false)
            {
                end += 1;
            }

            if (string.IsNullOrWhiteSpace(text) == false)
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

        int first;
        string[] searchFor;

        var doubleQuoteClass = text.IndexOf($"{search}\"", StringComparison.InvariantCultureIgnoreCase);
        var singleQuoteClass = text.IndexOf($"{search}'", StringComparison.InvariantCultureIgnoreCase);

        if (doubleQuoteClass == -1 || singleQuoteClass == -1)
        {
            first = Math.Max(doubleQuoteClass, singleQuoteClass);
        }
        else
        {
            first = Math.Min(doubleQuoteClass, singleQuoteClass);
        }

        if (first == -1)
        {
            searchFor = [$"{search}\"", $"{search}'"];
        }
        else if (doubleQuoteClass == first)
        {
            searchFor = [$"{search}\""];
        }
        else
        {
            searchFor = [$"{search}'"];
        }

        if (string.IsNullOrWhiteSpace(text) || first == -1 || string.IsNullOrWhiteSpace(text.Substring(0, first)) == false)
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

            while (start > 0 && !searchFor.Contains(start.Snapshot.GetText(start, Math.Min(start.Snapshot.Length - start, search.Length)).ToLower()))
            {
                start -= 1;
            }

            span = new SnapshotSpan(start, span.End);
        }

        SnapshotPoint segmentStart;
        var segmentEnd = span.Start;

        text = span.GetText().ToLower();

        if (text.Contains($"{search}\"") == false && text.Contains($"{search}'") == false)
        {
            yield break;
        }

        var index = segmentEnd - span.Start;

        while (index < text.Length && (text.IndexOf($"{search}\"", index) != -1 || text.IndexOf($"{search}'", index) != -1))
        {
            doubleQuoteClass = text.IndexOf($"{search}\"", index, StringComparison.InvariantCultureIgnoreCase);
            singleQuoteClass = text.IndexOf($"{search}'", index, StringComparison.InvariantCultureIgnoreCase);

            if (doubleQuoteClass == -1 || singleQuoteClass == -1)
            {
                segmentStart = new SnapshotPoint(snapshot, span.Start + Math.Max(doubleQuoteClass, singleQuoteClass));
            }
            else
            {
                segmentStart = new SnapshotPoint(snapshot, span.Start + Math.Min(doubleQuoteClass, singleQuoteClass));
            }

            char end;

            if (segmentStart == span.Start + doubleQuoteClass)
            {
                end = '"';
            }
            else
            {
                end = '\'';
            }

            segmentEnd = segmentStart + search.Length + 1;

            while (segmentEnd < span.End && segmentEnd + 1 < snapshot.Length)
            {
                segmentEnd += 1;

                if (end == segmentEnd.GetChar())
                {
                    yield return new SnapshotSpan(segmentStart, segmentEnd);

                    if (segmentEnd == span.End)
                    {
                        yield break;
                    }

                    break;
                }
            }

            index = segmentEnd - span.Start;
        }
    }
}
