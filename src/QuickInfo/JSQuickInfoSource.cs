using Microsoft.VisualStudio.Text;

namespace TailwindCSSIntellisense.QuickInfo;

internal class JSQuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator) : HtmlQuickInfoSource(textBuffer, descriptionGenerator)
{
    protected override string ClassKeywordToSearchFor => "className=\"";
}