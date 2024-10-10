using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TailwindCSSIntellisense.Parsers;
internal static class RazorParser
{
    public static IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
    {
        char[] endings = ['"', '\''];

        var text = span.GetText();
        var last = text.LastIndexOfAny(endings);

        // The goal of this method is to split a larger SnapshotSpan into smaller SnapshotSpans
        // Each smaller SnapshotSpan will be a segment between class=" and ", or class=' and ', and snapshot boundaries

        if (span.End != snapshot.Length && (string.IsNullOrWhiteSpace(text) || last == -1 || string.IsNullOrWhiteSpace(text.Substring(last + 1)) == false))
        {
            SnapshotPoint end = span.End;

            bool isInRazor = false;
            int depth = 0;
            // Number of quotes (excluding \")
            // Odd if in string context, even if not
            int numberOfQuotes = 0;
            bool isEscaping = false;

            while (end < snapshot.Length - 1 && (depth != 0 || numberOfQuotes % 2 == 1 || endings.Contains(end.GetChar()) == false))
            {
                var character = end.GetChar();

                if (character == '@')
                {
                    isInRazor = true;
                }
                else if (isInRazor)
                {
                    bool escape = isEscaping;
                    isEscaping = false;

                    if (numberOfQuotes % 2 == 1)
                    {
                        if (character == '\\')
                        {
                            isEscaping = true;
                        }
                    }
                    else
                    {
                        if (character == '(')
                        {
                            depth++;
                        }
                        else if (character == ')')
                        {
                            depth--;
                        }
                    }

                    if (character == '"' && !escape)
                    {
                        numberOfQuotes++;
                    }

                    if (depth == 0 && numberOfQuotes % 2 == 0 && character == ' ')
                    {
                        isInRazor = false;
                    }
                }

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

        var doubleQuoteClass = text.IndexOf("class=\"", StringComparison.InvariantCultureIgnoreCase);
        var singleQuoteClass = text.IndexOf("class='", StringComparison.InvariantCultureIgnoreCase);

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
            searchFor = ["class=\"", "class='"];
        }
        else if (doubleQuoteClass == first)
        {
            searchFor = ["class=\""];
        }
        else
        {
            searchFor = ["class='"];
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

            while (start > 0 && !searchFor.Contains(start.Snapshot.GetText(start, Math.Min(start.Snapshot.Length - start, 7)).ToLower()))
            {
                start -= 1;
            }

            span = new SnapshotSpan(start, span.End);
        }

        var segmentStart = span.Start;
        var segmentEnd = span.Start;

        text = span.GetText().ToLower();

        if (text.Contains("class=\"") == false && text.Contains("class='") == false)
        {
            yield break;
        }

        var index = segmentEnd - span.Start;

        while (index < text.Length && (text.IndexOf("class=\"", index) != -1 || text.IndexOf("class='", index) != -1))
        {
            doubleQuoteClass = text.IndexOf("class=\"", index, StringComparison.InvariantCultureIgnoreCase);
            singleQuoteClass = text.IndexOf("class='", index, StringComparison.InvariantCultureIgnoreCase);

            if (doubleQuoteClass == -1 || singleQuoteClass == -1)
            {
                segmentStart = new SnapshotPoint(snapshot, span.Start + Math.Max(doubleQuoteClass, singleQuoteClass));
            }
            else
            {
                segmentStart = new SnapshotPoint(snapshot, span.Start + Math.Min(doubleQuoteClass, singleQuoteClass));
            }

            char end;

            if (doubleQuoteClass == segmentStart)
            {
                end = '"';
            }
            else
            {
                end = '\'';
            }

            segmentEnd = segmentStart + 8;

            bool isInRazor = false;
            int depth = 0;
            // Number of quotes (excluding \")
            // Odd if in string context, even if not
            int numberOfQuotes = 0;
            char lastChar = default;

            while (segmentEnd < span.End && segmentEnd + 1 < snapshot.Length)
            {
                segmentEnd += 1;
                var character = segmentEnd.GetChar();

                if (character == '@')
                {
                    if (lastChar == '@')
                    {
                        isInRazor = false;
                        continue;
                    }

                    isInRazor = true;
                }

                if (isInRazor)
                {
                    bool escape = lastChar == '\\';

                    if (numberOfQuotes % 2 == 0)
                    {
                        if (character == '(')
                        {
                            depth++;
                        }
                        else if (character == ')')
                        {
                            depth--;
                        }
                    }

                    if (character == '"' && !escape)
                    {
                        numberOfQuotes++;
                    }

                    if (depth == 0 && numberOfQuotes % 2 == 0 && char.IsWhiteSpace(character))
                    {
                        isInRazor = false;
                    }

                    lastChar = character;
                    continue;
                }

                if (end == character)
                {
                    yield return new SnapshotSpan(segmentStart, segmentEnd);

                    if (segmentEnd == span.End)
                    {
                        yield break;
                    }

                    segmentStart = segmentEnd + 1;
                    break;
                }

                lastChar = character;
            }

            index = segmentEnd - span.Start;
        }
    }
}
