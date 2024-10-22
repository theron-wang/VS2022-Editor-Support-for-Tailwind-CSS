using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Adornments.Taggers;

[Export(typeof(IViewTaggerProvider))]
[TagType(typeof(IntraTextAdornmentTag))]
[ContentType("css")]
[ContentType("tcss")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal sealed class ColorCssTaggerProvider : IViewTaggerProvider
{
    [Import]
    internal CompletionUtilities CompletionUtilities { get; set; }

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        return buffer.Properties.GetOrCreateSingletonProperty(() => new ColorCssTagger(buffer, textView, CompletionUtilities)) as ITagger<T>;
    }

    private class ColorCssTagger(ITextBuffer buffer, ITextView view, CompletionUtilities completionUtilities)
        : ColorTaggerBase(buffer, view, completionUtilities)
    {
        protected override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
        {
            foreach (var scope in CssParser.GetScopes(span, snapshot))
            {
                // Find offset (i.e. space to @apply)
                var text = scope.GetText();

                int apply = text.IndexOf("@apply");

                // CSS parser does not guarantee it contains @apply
                if (apply == -1)
                {
                    continue;
                }

                // "@apply".Length + 1
                int offset = apply + 7;

                text = text.Substring(offset);

                // Now text contains a list of classes (separated by whitespace)

                var classes = text.Split((char[])[], StringSplitOptions.RemoveEmptyEntries);
                var index = -1;

                foreach (var @class in classes)
                {
                    // Keep track of index to account for duplicate classes
                    index = text.IndexOf(@class, index + 1);

                    yield return new SnapshotSpan(snapshot, scope.Start + offset + index, @class.Length);
                }
            }
        }
    }
}
