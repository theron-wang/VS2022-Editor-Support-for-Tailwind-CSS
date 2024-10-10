using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Linting.Validators;
internal class HtmlValidator : Validator
{
    protected virtual string SearchFor => $"class=";

    protected HtmlValidator(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities) : base(buffer, linterUtils, completionUtilities)
    {

    }

    public override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
    {
        return HtmlParser.GetScopes(span, snapshot);
    }

    public override IEnumerable<Error> GetErrors(SnapshotSpan span, bool force = false)
    {
        if (_checkedSpans.Contains(span) && !force)
        {
            yield break;
        }

        var text = span.GetText();

        bool doubleQuote = OnlyOneOccurance(text, $"{SearchFor}\"");
        bool singleQuote = OnlyOneOccurance(text, $"{SearchFor}'");

        #region Css conflict
        if (_linterUtils.GetErrorSeverity(ErrorType.CssConflict) != ErrorSeverity.None && doubleQuote != singleQuote)
        {
            text = GetFullScope(span, text);

            List<string> classes;

            if (doubleQuote)
            {
                classes =
                [
                    .. text.Substring(text.IndexOf($"{SearchFor}\"", StringComparison.InvariantCultureIgnoreCase) + SearchFor.Length + 1).Split('"')[0].Trim().Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries),
                ];
            }
            else
            {
                classes =
                [
                    .. text.Substring(text.IndexOf($"{SearchFor}'", StringComparison.InvariantCultureIgnoreCase) + SearchFor.Length + 1).Split('\'')[0].Trim().Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries),
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
        var first = text.IndexOf(item, StringComparison.InvariantCultureIgnoreCase);

        return first != -1 && text.IndexOf(item, first + 1, StringComparison.InvariantCultureIgnoreCase) == -1;
    }
}
