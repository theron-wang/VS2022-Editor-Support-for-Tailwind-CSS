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
[ContentType("JavaScript")]
[ContentType("TypeScript")]
[ContentType("jsx")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal sealed class ColorJSTaggerProvider : IViewTaggerProvider
{
    [Import]
    internal CompletionUtilities CompletionUtilities { get; set; }

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        return buffer.Properties.GetOrCreateSingletonProperty(() => new ColorJSTagger(buffer, textView, CompletionUtilities)) as ITagger<T>;
    }

    private class ColorJSTagger(ITextBuffer buffer, ITextView view, CompletionUtilities completionUtilities)
        : ColorTaggerBase(buffer, view, completionUtilities)
    {
        protected override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
        {
            foreach (var scope in JSParser.GetScopes(span, snapshot))
            {
                // Find offset (i.e. space to first quotation mark)

                var text = scope.GetText();

                int singleQuote = text.IndexOf('\'');
                int doubleQuote = text.IndexOf('\"');

                // GetScopes guarantees we will find at least one quote
                int offset = doubleQuote + 1;

                if (doubleQuote == -1 || (singleQuote != -1 && singleQuote < doubleQuote))
                {
                    offset = singleQuote + 1;
                }

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
