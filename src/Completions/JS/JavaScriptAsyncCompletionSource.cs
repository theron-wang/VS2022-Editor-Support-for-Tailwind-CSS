using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions.Sources;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.JS;
internal class JavaScriptAsyncCompletionSource : IAsyncCompletionSource
{
    private static ImageElement _icon = new(KnownMonikers.Field.ToImageId(), "Tailwind CSS Class");

    private readonly CompletionUtilities _completionUtilities;
    private readonly SettingsProvider _settingsProvider;

    private bool? _showAutocomplete;
    private bool _initializeSuccess = true;

    public JavaScriptAsyncCompletionSource(CompletionUtilities completionUtilities, SettingsProvider settingsProvider)
    {
        _completionUtilities = completionUtilities;
        _settingsProvider = settingsProvider;

        _settingsProvider.OnSettingsChanged += SettingsChangedAsync;
    }

    public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
    {
        if (IsInClassScope(triggerLocation.Snapshot, triggerLocation, out _) == false)
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
        if (_showAutocomplete == null)
        {
            _showAutocomplete = (await _settingsProvider.GetSettingsAsync()).EnableTailwindCss;
        }

        if (_showAutocomplete == false || _completionUtilities.Scanner.HasConfigurationFile == false)
        {
            return new CompletionContext(ImmutableArray<CompletionItem>.Empty, null);
        }

        if (!_completionUtilities.Initialized || _initializeSuccess == false)
        {
            _initializeSuccess = await _completionUtilities.InitializeAsync();

            if (_initializeSuccess == false)
            {
                return new CompletionContext(ImmutableArray<CompletionItem>.Empty, null);
            }
        }

        if (IsInClassScope(session.TextView.TextSnapshot, session.TextView.Caret.Position.BufferPosition, out var classText) == false)
        {
            return new CompletionContext(ImmutableArray<CompletionItem>.Empty, null);
        }

        applicableToSpan = GetApplicableTo(triggerLocation, session.TextView.TextSnapshot);

        var items = ClassCompletionGeneratorHelper.GetCompletions(classText.Split().Last(), _completionUtilities)
            .Select(c =>
            {
                var item = new CompletionItem(c.DisplayText, this, _icon, ImmutableArray<CompletionFilter>.Empty, null, c.InsertionText, c.DisplayText, c.DisplayText, null, ImmutableArray<ImageElement>.Empty, ImmutableArray<char>.Empty, applicableToSpan, false, false);
                item.Properties.AddProperty("description", c.Description);

                return item;
            });

        return new CompletionContext(items.ToImmutableArray(), null);
    }

    /// <summary>
    /// Provides detailed element information in the tooltip
    /// </summary>
    public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
    {
        if (item.Properties.TryGetProperty("description", out string description))
        {
            return Task.FromResult<object>(description);
        }
        return Task.FromResult<object>("");
    }


    private bool IsInClassScope(ITextSnapshot snapshot, SnapshotPoint trigger, out string classText)
    {
        if (snapshot != trigger.Snapshot)
        {
            classText = null;
            return false;
        }

        var startPos = new SnapshotPoint(snapshot, 0);

        var searchSnapshot = new SnapshotSpan(startPos, trigger);
        var text = searchSnapshot.GetText();

        var indexOfCurrentClassAttribute = text.LastIndexOf("className=\"", StringComparison.InvariantCultureIgnoreCase);
        if (indexOfCurrentClassAttribute == -1)
        {
            classText = null;
            return false;
        }
        var quotationMarkAfterLastClassAttribute = text.IndexOf('\"', indexOfCurrentClassAttribute);
        var lastQuotationMark = text.LastIndexOf('\"');

        if (lastQuotationMark == quotationMarkAfterLastClassAttribute)
        {
            classText = text.Substring(lastQuotationMark + 1);
            return true;
        }
        else
        {
            classText = null;
            return false;
        }
    }

    private SnapshotSpan GetApplicableTo(SnapshotPoint triggerPoint, ITextSnapshot snapshot)
    {
        SnapshotPoint end = triggerPoint;
        SnapshotPoint start = triggerPoint - 1;

        while (start.GetChar() != '"' && start.GetChar() != ' ')
        {
            start -= 1;
        }

        while (end.Position < snapshot.Length && end.GetChar() != '"' && end.GetChar() != '\'' && !char.IsWhiteSpace(end.GetChar()))
        {
            end += 1;
        }

        start += 1;

        return new SnapshotSpan(start, end);
    }
    private Task SettingsChangedAsync(TailwindSettings settings)
    {
        _showAutocomplete = settings.EnableTailwindCss;
        return Task.CompletedTask;
    }
}
