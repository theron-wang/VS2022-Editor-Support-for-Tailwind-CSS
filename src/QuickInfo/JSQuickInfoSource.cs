using Microsoft.VisualStudio.Text;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

internal class JSQuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator, CompletionUtilities completionUtilities) : HtmlQuickInfoSource(textBuffer, descriptionGenerator, completionUtilities)
{
    protected override string ClassKeywordToSearchFor => "className=\"";
}