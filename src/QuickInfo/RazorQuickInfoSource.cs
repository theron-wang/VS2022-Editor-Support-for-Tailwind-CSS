using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.QuickInfo;

internal class RazorQuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator, ProjectConfigurationManager completionUtilities, CompletionConfiguration completionConfiguration) : QuickInfoSource(textBuffer, descriptionGenerator, completionUtilities, completionConfiguration)
{
    protected override bool IsInClassScope(IAsyncQuickInfoSession session, out SnapshotSpan? span)
    {
        var searchPos = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);

        if (searchPos == null)
        {
            span = null;
            return false;
        }

        span = RazorParser.GetClassAttributeValue(searchPos.Value);
        return span.HasValue;
    }

    protected override string UnescapeClass(string input)
    {
        return input.Replace("@@", "@").Replace("@(\"@\")", "@");
    }
}
