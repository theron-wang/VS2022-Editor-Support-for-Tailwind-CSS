using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Adornments.Taggers;

[Export(typeof(IViewTaggerProvider))]
[TagType(typeof(IntraTextAdornmentTag))]
[ContentType("razor")]
[ContentType("LegacyRazorCSharp")]
[ContentType("LegacyRazor")]
[ContentType("LegacyRazorCoreCSharp")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal sealed class ColorRazorTaggerProvider : IViewTaggerProvider
{
    [Import]
    internal CompletionUtilities CompletionUtilities { get; set; }

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        return buffer.Properties.GetOrCreateSingletonProperty(() => new ColorRazorTagger(buffer, textView, CompletionUtilities)) as ITagger<T>;
    }

    private class ColorRazorTagger(ITextBuffer buffer, ITextView view, CompletionUtilities completionUtilities)
        : ColorTaggerBase(buffer, view, completionUtilities)
    {
        protected override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
        {
            foreach (var scope in RazorParser.GetScopes(span, snapshot))
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

                // The class text with all razor removed
                var unrazored = new StringBuilder();

                var isInRazor = false;
                char last = default;
                // Number of quotes (excluding \")
                // Odd if in string context, even if not
                int numberOfQuotes = 0;
                int depth = 0;

                foreach (var character in text)
                {
                    // Handle @@ escape sequence
                    if (isInRazor && last == '@' && character == '@')
                    {
                        unrazored.Append("@@");
                        isInRazor = false;
                        continue;
                    }

                    if (!isInRazor && character != '@')
                    {
                        unrazored.Append(character);
                    }

                    if (character == '@')
                    {
                        isInRazor = true;
                    }
                    else if (isInRazor)
                    {
                        bool escape = last == '\\';

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
                            unrazored.Append(character);
                        }
                    }

                    last = character;
                }

                // Now text contains a list of classes (separated by whitespace)

                var classes = unrazored.ToString().Split((char[])[], StringSplitOptions.RemoveEmptyEntries);
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
