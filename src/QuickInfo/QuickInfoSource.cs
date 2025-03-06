using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo;

internal abstract class QuickInfoSource : IAsyncQuickInfoSource
{
    protected ITextBuffer _textBuffer;
    protected DescriptionGenerator _descriptionGenerator;
    private readonly ProjectCompletionValues _completionUtilities;

    private const string PropertyKey = "tailwindintellisensequickinfoadded";

    public QuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator, CompletionUtilities completionUtilities)
    {
        _textBuffer = textBuffer;
        _descriptionGenerator = descriptionGenerator;
        _completionUtilities = completionUtilities.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());
    }

    public void Dispose()
    {
    }

    public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
        // session.Properties is to ensure that quick info is only added once (measure for #17)
        if (session.Content is null || session.Content.Any() || session.State == QuickInfoSessionState.Visible || session.State == QuickInfoSessionState.Dismissed || session.Properties.ContainsProperty(PropertyKey))
        {
            return Task.FromResult<QuickInfoItem>(null);
        }

        var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);

        if (triggerPoint != null && IsInClassScope(session, out var classSpan) && classSpan != null)
        {
            var fullText = classSpan.Value.GetText();
            var classText = fullText.Split(':').Last();

            var isImportant = ImportantModifierHelper.IsImportantModifier(classText);

            if (isImportant)
            {
                classText = classText.TrimStart('!');
            }

            if (!_completionUtilities.IsClassAllowed(classText))
            {
                return Task.FromResult<QuickInfoItem>(null);
            }

            var desc = _descriptionGenerator.GetDescription(classText, _completionUtilities);

            var span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(classSpan.Value, SpanTrackingMode.EdgeInclusive);

            if (string.IsNullOrEmpty(desc) == false)
            {
                session.Properties.AddProperty(PropertyKey, true);

                var totalVariant = fullText.Contains(':') ?
                    _descriptionGenerator.GetTotalVariantDescription(fullText.Substring(0, fullText.Length - classText.Length - 1), _completionUtilities) :
                    [];

                ContainerElement descriptionFormatted;

                if (_completionUtilities.Version == TailwindVersion.V3)
                {
                    descriptionFormatted = DescriptionUIHelper.GetDescriptionAsUIFormatted(fullText,
                            totalVariant.LastOrDefault(),
                            totalVariant.Length > 1 ? totalVariant.Take(totalVariant.Length - 1).ToArray() : [],
                            desc, isImportant);
                }
                else
                {
                    descriptionFormatted = DescriptionUIHelper.GetDescriptionAsUIFormattedV4(fullText, totalVariant.FirstOrDefault(), desc, isImportant);
                }

                return Task.FromResult(new QuickInfoItem(span, descriptionFormatted));
            }
        }

        return Task.FromResult<QuickInfoItem>(null);
    }

    protected abstract bool IsInClassScope(IAsyncQuickInfoSession session, out SnapshotSpan? span);
}
