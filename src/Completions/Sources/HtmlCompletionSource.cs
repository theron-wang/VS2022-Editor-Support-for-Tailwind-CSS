﻿using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using TailwindCSSIntellisense.Configuration;
using TailwindCSSIntellisense.Options;
using TailwindCSSIntellisense.Parsers;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Sources;

/// <summary>
/// Completion provider for all HTML content files to provide Intellisense support for TailwindCSS classes
/// </summary>
internal class HtmlCompletionSource(ITextBuffer textBuffer, ProjectConfigurationManager completionUtils, ColorIconGenerator colorIconGenerator, DescriptionGenerator descriptionGenerator, SettingsProvider settingsProvider, CompletionConfiguration completionConfiguration) :
    ClassCompletionGenerator(textBuffer, completionUtils, colorIconGenerator, descriptionGenerator, settingsProvider, completionConfiguration), ICompletionSource
{
    private bool _initializeSuccess = true;

    /// <summary>
    /// Overrides the original completion set to include TailwindCSS classes
    /// </summary>
    /// <param name="session">Provided by Visual Studio</param>
    /// <param name="completionSets">Provided by Visual Studio</param>
    void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
    {
        var settings = _settingsProvider.GetSettings();

        _showAutocomplete ??= settings.EnableTailwindCss;

        if (_showAutocomplete == false || settings.ConfigurationFiles.Count == 0)
        {
            return;
        }

        if (!_completionUtils.Initialized || _initializeSuccess == false)
        {
            _initializeSuccess = ThreadHelper.JoinableTaskFactory.Run(_completionUtils.InitializeAsync);

            if (_initializeSuccess == false)
            {
                return;
            }
        }

        if (HtmlParser.IsCursorInClassScope(session.TextView, out var classSpan) == false || classSpan is null)
        {
            return;
        }

        var truncatedClassSpan = new SnapshotSpan(classSpan.Value.Start, session.TextView.Caret.Position.BufferPosition);
        string classAttributeValueUpToPosition = truncatedClassSpan.GetText();

        var position = session.TextView.Caret.Position.BufferPosition.Position;
        var snapshot = _textBuffer.CurrentSnapshot;
        var triggerPoint = session.GetTriggerPoint(snapshot);

        if (triggerPoint == null)
        {
            return;
        }

        var applicableTo = GetApplicableTo(triggerPoint.Value, snapshot);
        var currentClassTotal = classAttributeValueUpToPosition.Split(' ').Last();

        var completions = GetCompletions(applicableTo.GetText(snapshot));

        if (completionSets.Count == 1)
        {
            var defaultCompletionSet = completionSets[0];

            if (defaultCompletionSet.Completions.Count > 0)
            {
                var addToBeginning = ThreadHelper.JoinableTaskFactory.Run(General.GetLiveInstanceAsync).TailwindCompletionsComeFirst;

                if (addToBeginning)
                {
                    // Cast to Completion3 to gain access to IconMoniker
                    // Return new Completion3 so session commit will actually commit the text
                    completions.AddRange(defaultCompletionSet.Completions
                        .Where(c => c.DisplayText.StartsWith(currentClassTotal, StringComparison.InvariantCultureIgnoreCase))
                        .Cast<Completion3>()
                        .Select(c => new Completion3(c.DisplayText, c.InsertionText, c.DisplayText, new ImageMoniker() { Guid = c.IconMoniker.Guid, Id = c.IconMoniker.Id }, c.IconAutomationText)));
                }
                else
                {
                    completions.InsertRange(0, defaultCompletionSet.Completions
                        .Where(c => c.DisplayText.StartsWith(currentClassTotal, StringComparison.InvariantCultureIgnoreCase))
                        .Cast<Completion3>()
                        .Select(c => new Completion3(c.DisplayText, c.InsertionText, c.DisplayText, new ImageMoniker() { Guid = c.IconMoniker.Guid, Id = c.IconMoniker.Id }, c.IconAutomationText)));
                }
            }

            var overridenCompletionSet = new TailwindCssCompletionSet(
                defaultCompletionSet.Moniker,
                defaultCompletionSet.DisplayName,
                applicableTo,
                completions,
                defaultCompletionSet.CompletionBuilders);
            // Overrides the original completion set so there aren't two different completion tabs
            completionSets.Clear();
            completionSets.Add(overridenCompletionSet);
        }
        else
        {
            completionSets.Add(new TailwindCssCompletionSet(
                "All",
                "All",
                applicableTo,
                completions,
                new List<Completion>()));
        }
    }

    private ITrackingSpan GetApplicableTo(SnapshotPoint triggerPoint, ITextSnapshot snapshot)
    {
        var span = HtmlParser.GetClassAttributeValue(triggerPoint);
        // span should not be null since this is called after we verify the cursor is in a class context
        return snapshot.CreateTrackingSpan(new SnapshotSpan(span!.Value.Start, triggerPoint), SpanTrackingMode.EdgeInclusive);
    }
}
