using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Linq;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.QuickInfo;

internal class RazorQuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator, ProjectConfigurationManager completionUtilities) : QuickInfoSource(textBuffer, descriptionGenerator, completionUtilities)
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
}
