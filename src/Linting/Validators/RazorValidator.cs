using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Linting.Validators;
internal class RazorValidator : Validator
{
    private RazorValidator(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities) : base(buffer, linterUtils, completionUtilities)
    {

    }

    public override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
    {
        return RazorParser.GetScopes(span, snapshot);
    }

    public override IEnumerable<Error> GetErrors(SnapshotSpan span, bool force = false)
    {
        if (_checkedSpans.Any(s => s.Equals(span)) && !force)
        {
            yield break;
        }

        var text = span.GetText();

        bool doubleQuote = OnlyOneOccurance(text, "class=\"");
        bool singleQuote = OnlyOneOccurance(text, "class='");

        #region Css conflict
        if (_linterUtils.GetErrorSeverity(ErrorType.CssConflict) != ErrorSeverity.None && doubleQuote != singleQuote)
        {
            (string classText, text) = GetFullScope(span, text);

            if (string.IsNullOrWhiteSpace(classText) || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            List<string> classes;

            if (doubleQuote)
            {
                classes =
                [
                    .. classText.Substring(classText.IndexOf("class=\"", StringComparison.InvariantCultureIgnoreCase) + 7).Split('"')[0].Trim().Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries),
                ];
            }
            else
            {
                classes =
                [
                    .. classText.Substring(classText.IndexOf("class='", StringComparison.InvariantCultureIgnoreCase) + 7).Split('\'')[0].Trim().Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries),
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

                    if (_checkedSpans.Contains(errorSpan) == false)
                    {
                        _checkedSpans.Add(errorSpan);
                        yield return new Error(errorSpan, errorMessage, ErrorType.CssConflict);
                    }
                }
            }
            _checkedSpans.Add(span);
        }
        #endregion
    }

    public static Validator Create(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities)
    {
        return buffer.Properties.GetOrCreateSingletonProperty<Validator>(() => new RazorValidator(buffer, linterUtils, completionUtilities));
    }

    private (string scope, string classText) GetFullScope(SnapshotSpan span, string text)
    {
        var start = span.Span.Start;

        char[] endings = ['"', '\''];

        var quote1 = text.IndexOfAny(endings);

        text = text.Substring(0, quote1 + 1);

        SnapshotPoint end = span.Start + quote1 + 1;

        bool isInRazor = false;
        int depth = 0;
        // Number of quotes (excluding \")
        // Odd if in string context, even if not
        int numberOfQuotes = 0;
        bool isEscaping = false;

        while (end < span.Snapshot.Length - 1 && (depth != 0 || numberOfQuotes % 2 == 1 || endings.Contains(end.GetChar()) == false))
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
                    text += character;
                    isInRazor = false;
                }
            }
            else
            {
                text += character;
            }

            end += 1;
        }

        if (depth != 0 || numberOfQuotes % 2 == 1)
        {
            return (null, null);
        }

        return (text, span.Snapshot.GetText(start, end - start));
    }

    private bool OnlyOneOccurance(string text, string item)
    {
        return text.Contains(item) && text.IndexOf(item, text.IndexOf(item, StringComparison.InvariantCultureIgnoreCase) + 1, StringComparison.InvariantCultureIgnoreCase) == -1;
    }
}
