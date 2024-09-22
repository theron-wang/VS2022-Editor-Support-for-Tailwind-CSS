using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Linq;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

internal class HtmlQuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator, CompletionUtilities completionUtilities) : QuickInfoSource(textBuffer, descriptionGenerator, completionUtilities)
{
    protected virtual string ClassKeywordToSearchFor => "class=\"";

    protected override bool IsInClassScope(IAsyncQuickInfoSession session, out SnapshotSpan? span)
    {
        var startPos = new SnapshotPoint(_textBuffer.CurrentSnapshot, 0);
        var searchPos = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);

        if (searchPos == null)
        {
            span = null;
            return false;
        }

        var searchSnapshot = new SnapshotSpan(startPos, searchPos.Value);
        var text = searchSnapshot.GetText();

        var indexOfCurrentClassAttribute = text.LastIndexOf(ClassKeywordToSearchFor, StringComparison.InvariantCultureIgnoreCase);
        if (indexOfCurrentClassAttribute == -1)
        {
            span = null;
            return false;
        }

        var quotationMarkAfterLastClassAttribute = text.IndexOf('\"', indexOfCurrentClassAttribute);
        var lastQuotationMark = text.LastIndexOf('\"');

        if (lastQuotationMark == quotationMarkAfterLastClassAttribute)
        {
            var startIndex = lastQuotationMark + 1;
            text = text.Substring(lastQuotationMark + 1);
            startIndex += text.LastIndexOf(' ') == -1 ? 0 : text.LastIndexOf(' ') + 1;
            var length = 1;

            searchSnapshot = new SnapshotSpan(_textBuffer.CurrentSnapshot, startIndex, length);
            var last = searchSnapshot.GetText().Last();

            while (char.IsWhiteSpace(last) == false && last != '"' && last != '\'')
            {
                length++;
                searchSnapshot = new SnapshotSpan(_textBuffer.CurrentSnapshot, startIndex, length);
                last = searchSnapshot.GetText().Last();
            }

            searchSnapshot = new SnapshotSpan(_textBuffer.CurrentSnapshot, startIndex, length - 1);

            span = searchSnapshot;

            return true;
        }
        else
        {
            span = null;
            return false;
        }
    }
}