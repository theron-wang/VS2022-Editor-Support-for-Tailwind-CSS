using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Operations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

internal class CssDirectiveQuickInfoSource : IAsyncQuickInfoSource
{
    protected ITextBuffer _textBuffer;
    private readonly ProjectCompletionValues _completionUtilities;
    private readonly ITextStructureNavigator _textStructureNavigator;


    public CssDirectiveQuickInfoSource(ITextBuffer textBuffer, CompletionUtilities completionUtilities, ITextStructureNavigatorSelectorService textStructureNavigatorSelectorService)
    {
        _textBuffer = textBuffer;
        _completionUtilities = completionUtilities.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());
        _textStructureNavigator = textStructureNavigatorSelectorService.GetTextStructureNavigator(_textBuffer);
    }

    public void Dispose()
    {
    }

    public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
        if (session.Content is null || session.Content.Any() || session.State == QuickInfoSessionState.Visible || session.State == QuickInfoSessionState.Dismissed)
        {
            return Task.FromResult<QuickInfoItem>(null);
        }

        var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);

        if (triggerPoint is null)
        {
            return Task.FromResult<QuickInfoItem>(null);
        }

        var extent = _textStructureNavigator.GetExtentOfWord(triggerPoint.Value);

        if (extent.IsSignificant)
        {
            var text = extent.Span.GetText();

            if (text == "@apply")
            {
            }
            else if (_completionUtilities.Version == TailwindVersion.V3 && (text == "@tailwind" || text == "@config"))
            {
            }
            else if (text == "@theme" || text == "@source" || text == "@utility" || text == "@custom-variant" || text == "@config" || text == "@plugin" || text == "@variant" || text.StartsWith("@slot"))
            {
            }
            else
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            var element = new ContainerElement(
                ContainerElementStyle.Stacked,
                new ClassifiedTextElement(
                        new ClassifiedTextRun(
                            PredefinedClassificationTypeNames.Type, 
                            $"{text} is a valid Tailwind directive. Please disregard the error.",
                            ClassifiedTextRunStyle.Bold
                )));

            var span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);

            return Task.FromResult(new QuickInfoItem(span, element));
        }

        return Task.FromResult<QuickInfoItem>(null);
    }
}
