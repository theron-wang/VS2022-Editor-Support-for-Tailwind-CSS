﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Linting.Validators;
internal abstract class HtmlLikeValidator(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities)
    : Validator(buffer, linterUtils, completionUtilities)
{
    protected abstract Func<string, IEnumerable<Match>> ClassSplitter { get; set; }
    protected abstract Func<string, string, IEnumerable<Match>> ClassMatchGetter { get; set; }

    public override IEnumerable<Error> GetErrors(SnapshotSpan span, bool force = false)
    {
        if (_checkedSpans.Any(s => s.Equals(span)) && !force)
        {
            yield break;
        }

        #region Css conflict
        if (_linterUtils.GetErrorSeverity(ErrorType.CssConflict) != ErrorSeverity.None)
        {
            (var scope, var content) = GetFullScope(span);

            if (string.IsNullOrWhiteSpace(content))
            {
                yield break;
            }

            List<string> classes = ClassSplitter(content).Select(c => c.Value).ToList();

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
                    index = scope.IndexOf(className, index + 1);

                    if (index == -1)
                    {
                        index = scope.IndexOf(className, index + 1);
                    }

                    while (index != -1 && index + className.Length < scope.Length && !char.IsWhiteSpace(scope[index + className.Length]) && !"\"'".Contains(scope[index + className.Length]))
                    {
                        index = scope.IndexOf(className, index + 1);
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

    /// <summary>
    /// Gets the full scope of the class, including the content within
    /// Scope = class="class1 class2"
    /// Content = class1 class2
    /// </summary>
    private (string scope, string content) GetFullScope(SnapshotSpan span)
    {
        // If it already exists, do not expand scope
        var text = span.GetText();

        foreach (var match in ClassMatchGetter(text, text))
        {
            if (span.Start.Position >= match.Index && span.Start.Position <= match.Index + match.Length)
            {
                var group = ClassRegexHelper.GetClassTextGroup(match);
                return (match.Value, group.Value);
            }
        }

        var start = Math.Max(0, (int)span.Start - 2000);

        span = new SnapshotSpan(span.Snapshot, start, Math.Min(span.Snapshot.Length, (int)span.End + 2000) - start);

        text = span.GetText();

        foreach (var match in ClassMatchGetter(text, text))
        {
            if (span.Start.Position >= match.Index && span.Start.Position <= match.Index + match.Length)
            {
                var group = ClassRegexHelper.GetClassTextGroup(match);
                return (match.Value, group.Value);
            }
        }

        return (null, null);
    }
}
