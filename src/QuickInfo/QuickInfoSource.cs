﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Configuration;

namespace TailwindCSSIntellisense.QuickInfo;

internal abstract class QuickInfoSource : IAsyncQuickInfoSource
{
    protected ITextBuffer _textBuffer;
    protected DescriptionGenerator _descriptionGenerator;
    private readonly ProjectConfigurationManager _projectConfigurationManager;
    private readonly CompletionConfiguration _completionConfiguration;
    private ProjectCompletionValues _projectConfigurationValues;

    private const string PropertyKey = "tailwindintellisensequickinfoadded";

    public QuickInfoSource(ITextBuffer textBuffer, DescriptionGenerator descriptionGenerator, ProjectConfigurationManager projectConfigurationManager, CompletionConfiguration completionConfiguration)
    {
        _textBuffer = textBuffer;
        _descriptionGenerator = descriptionGenerator;
        _projectConfigurationManager = projectConfigurationManager;
        _completionConfiguration = completionConfiguration;
        _completionConfiguration.ConfigurationUpdated += OnConfigurationUpdated;
        _projectConfigurationValues = projectConfigurationManager.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());
    }

    private void OnConfigurationUpdated()
    {
        _projectConfigurationValues = _projectConfigurationManager.GetCompletionConfigurationByFilePath(_textBuffer.GetFileName());
    }

    public void Dispose()
    {
        _completionConfiguration.ConfigurationUpdated -= OnConfigurationUpdated;
    }

    public Task<QuickInfoItem?> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
        // session.Properties is to ensure that quick info is only added once (measure for #17)
        if (session.Content is null || session.Content.Any() || session.State == QuickInfoSessionState.Visible || session.State == QuickInfoSessionState.Dismissed || session.Properties.ContainsProperty(PropertyKey))
        {
            return Task.FromResult<QuickInfoItem?>(null);
        }

        var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);

        if (triggerPoint != null && IsInClassScope(session, out var classSpan) && classSpan != null)
        {
            var fullText = classSpan.Value.GetText();
            var unescapedFullText = UnescapeClass(fullText);

            if (!_projectConfigurationValues.IsClassAllowed(unescapedFullText))
            {
                return Task.FromResult<QuickInfoItem?>(null);
            }

            var desc = _descriptionGenerator.GetDescription(unescapedFullText, _projectConfigurationValues);

            var span = _textBuffer.CurrentSnapshot.CreateTrackingSpan(classSpan.Value, SpanTrackingMode.EdgeInclusive);

            if (string.IsNullOrEmpty(desc) == false)
            {
                session.Properties.AddProperty(PropertyKey, true);

                var totalVariant = unescapedFullText.Contains(':') ?
                    _descriptionGenerator.GetTotalVariantDescription(unescapedFullText.Substring(0, unescapedFullText.LastIndexOf(':')), _projectConfigurationValues) :
                    [];

                ContainerElement descriptionFormatted;

                if (_projectConfigurationValues.Version == TailwindVersion.V3)
                {
                    descriptionFormatted = DescriptionUIHelper.GetDescriptionAsUIFormatted(fullText,
                            totalVariant.LastOrDefault(),
                            totalVariant.Length > 1 ? [.. totalVariant.Take(totalVariant.Length - 1)] : [],
                            desc!);
                }
                else
                {
                    descriptionFormatted = DescriptionUIHelper.GetDescriptionAsUIFormattedV4(fullText, totalVariant.FirstOrDefault(), desc!);
                }

                return Task.FromResult<QuickInfoItem?>(new QuickInfoItem(span, descriptionFormatted));
            }
        }

        return Task.FromResult<QuickInfoItem?>(null);
    }

    protected abstract bool IsInClassScope(IAsyncQuickInfoSession session, out SnapshotSpan? span);

    protected virtual string UnescapeClass(string input)
    {
        return input;
    }
}
