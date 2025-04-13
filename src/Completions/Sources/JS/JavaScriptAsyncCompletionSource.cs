using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.CodeCleanUp;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Parsers;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Sources.JS;
internal class JavaScriptAsyncCompletionSource(ITextBuffer buffer, ProjectConfigurationManager completionUtils, ColorIconGenerator colorIconGenerator, DescriptionGenerator descriptionGenerator, SettingsProvider settingsProvider) :
    ClassCompletionGenerator(buffer, completionUtils, colorIconGenerator, descriptionGenerator, settingsProvider), IAsyncCompletionSource
{
    private static readonly ImageElement _icon = new(KnownMonikers.Field.ToImageId(), "Tailwind CSS Class");

    private bool _initializeSuccess = true;

    public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
    {
        if (JSParser.IsInClassScope(triggerLocation.Snapshot, triggerLocation, out _) == false)
        {
            return CompletionStartData.DoesNotParticipateInCompletion;
        }

        if (trigger.Reason == CompletionTriggerReason.Insertion || char.IsWhiteSpace(triggerLocation.GetChar()))
        {
            return CompletionStartData.ParticipatesInCompletionIfAny;
        }

        if (triggerLocation.GetChar() == '"' && triggerLocation.Position > 0)
        {
            triggerLocation -= 1;
        }

        var applicableTo = GetApplicableTo(triggerLocation, triggerLocation.Snapshot);
        return new CompletionStartData(CompletionParticipation.ProvidesItems, applicableTo);
    }

    public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
    {
        var settings = await _settingsProvider.GetSettingsAsync();

        _showAutocomplete ??= settings.EnableTailwindCss;

        if (_showAutocomplete == false || settings.ConfigurationFiles.Count == 0)
        {
            return new CompletionContext(ImmutableArray<CompletionItem>.Empty, null);
        }

        if (!_completionUtils.Initialized || _initializeSuccess == false)
        {
            _initializeSuccess = await _completionUtils.InitializeAsync();

            if (_initializeSuccess == false)
            {
                return new CompletionContext(ImmutableArray<CompletionItem>.Empty, null);
            }
        }

        if (JSParser.IsCursorInClassScope(session.TextView, out var classSpan) == false || classSpan is null)
        {
            return new CompletionContext(ImmutableArray<CompletionItem>.Empty, null);
        }

        var truncatedClassSpan = new SnapshotSpan(classSpan.Value.Start, session.TextView.Caret.Position.BufferPosition);
        string classText = truncatedClassSpan.GetText();

        var items = GetCompletions(applicableToSpan.GetText())
            .Select(c =>
            {
                var item = new CompletionItem(c.DisplayText, this, _icon, ImmutableArray<CompletionFilter>.Empty, null, c.InsertionText, c.InsertionText, c.InsertionText, null, ImmutableArray<ImageElement>.Empty, ImmutableArray<char>.Empty, applicableToSpan, false, false);
                item.Properties.AddProperty("description-text", c.Description);

                return item;
            });

        return new CompletionContext(items.ToImmutableArray(), null);
    }

    /// <summary>
    /// Provides detailed element information in the tooltip
    /// </summary>
    public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
    {
        if (item.Properties.TryGetProperty("description-text", out string description))
        {
            return Task.FromResult<object>(_descriptionGenerator.GetDescription(description, _projectCompletionValues));
        }
        return Task.FromResult<object>("");
    }
    private SnapshotSpan GetApplicableTo(SnapshotPoint triggerPoint, ITextSnapshot snapshot)
    {
        var span = JSParser.GetClassAttributeValue(triggerPoint);

        var end = triggerPoint;

        if ((int)end + 1 < snapshot.Length && span.Value.Contains(end))
        {
            end += 1;
        }

        // Trigger point should be included
        return new SnapshotSpan(span.Value.Start, end);
    }
}
