using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Linting.Taggers;

namespace TailwindCSSIntellisense.Linting.Validators;
internal class HtmlValidator : Validator
{
    private HtmlValidator(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities) : base(buffer, linterUtils, completionUtilities)
    {

    }

    public override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
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

            while (start > 0 && !searchFor.Contains(start.Snapshot.GetText(start, Math.Min(start.Snapshot.Length - start, 6)).ToLower()))
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

        while (text.IndexOf("class=\"", index) != -1 || text.IndexOf("class='", index) != -1)
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

                    segmentStart = segmentEnd + 1;
                    break;
                }
            }

            index = segmentEnd - span.Start;
        }
    }

    public override IEnumerable<Error> GetErrors(SnapshotSpan span, bool force = false)
    {
        if (_checkedSpans.Contains(span) && !force)
        {
            yield break;
        }

        var text = span.GetText();

        bool doubleQuote = OnlyOneOccurance(text, "class=\"");
        bool singleQuote = OnlyOneOccurance(text, "class='");

        #region Css conflict
        if (_linterUtils.GetErrorSeverity(ErrorType.CssConflict) != ErrorSeverity.None && doubleQuote != singleQuote)
        {
            text = GetFullScope(span, text);

            List<string> classes;

            if (doubleQuote)
            {
                classes =
                [
                    .. text.Substring(text.IndexOf("class=\"", StringComparison.InvariantCultureIgnoreCase) + 7).Split('"')[0].Trim().Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries),
                ];
            }
            else
            {
                classes =
                [
                    .. text.Substring(text.IndexOf("class='", StringComparison.InvariantCultureIgnoreCase) + 7).Split('\'')[0].Trim().Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries),
                ];
            }

            var classesByModifiers = classes.GroupBy(c =>
            {
                var index = c.LastIndexOf(':');

                if (index == -1)
                {
                    return "";
                }
                return string.Join(":", c.Substring(0, index).Split(':').OrderBy(x => x));
            });

            foreach (var grouping in classesByModifiers)
            {
                // Find duplicates, since we are parsing from left to right
                int index = -1;
                foreach ((var className, var errorMessage) in _linterUtils.CheckForClassDuplicates(grouping))
                {
                    index = text.IndexOf(className, index + 1);

                    if (index == -1)
                    {
                        index = text.IndexOf(className, index + 1);
                    }

                    while (index != -1 && index + className.Length < text.Length && !char.IsWhiteSpace(text[index + className.Length]) && !"\"'".Contains(text[index + className.Length]))
                    {
                        index = text.IndexOf(className, index + 1);
                    }

                    if (index == -1)
                    {
                        continue;
                    }

                    var errorSpan = new SnapshotSpan(_buffer.CurrentSnapshot, span.Span.Start + index, className.Length);

                    _checkedSpans.Add(span);
                    yield return new Error(errorSpan, errorMessage, ErrorType.CssConflict);
                }
            }
        }
        #endregion
    }

    public static Validator Create(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities)
    {
        return buffer.Properties.GetOrCreateSingletonProperty<Validator>(() => new HtmlValidator(buffer, linterUtils, completionUtilities));
    }

    private string GetFullScope(SnapshotSpan span, string text)
    {
        var start = span.Span.Start;
        var length = span.Span.Length;

        var quote1 = text.IndexOfAny(['"', '\'']);

        while (start + length < span.Snapshot.Length && text.IndexOfAny(['"', '\''], quote1 + 1) == -1)
        {
            length++;
            text = span.Snapshot.GetText(start, length);
        }

        return text;
    }

    private bool OnlyOneOccurance(string text, string item)
    {
        return text.Contains(item) && text.IndexOf(item, text.IndexOf(item, StringComparison.InvariantCultureIgnoreCase) + 1, StringComparison.InvariantCultureIgnoreCase) == -1;
    }
}
