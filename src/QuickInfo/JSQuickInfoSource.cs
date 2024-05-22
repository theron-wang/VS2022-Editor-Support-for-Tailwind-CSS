using Microsoft.VisualStudio.Text;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo
{
    internal class JSQuickInfoSource(ITextBuffer textBuffer, CompletionUtilities completionUtilities) : HtmlQuickInfoSource(textBuffer, completionUtilities)
    {
        protected override string ClassKeywordToSearchFor => "className=\"";
    }
}