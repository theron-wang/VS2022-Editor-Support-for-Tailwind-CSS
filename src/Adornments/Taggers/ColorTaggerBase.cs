﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Options;
using Microsoft.VisualStudio.Shell;

namespace TailwindCSSIntellisense.Adornments.Taggers;

/// <summary>
/// Code adapted from https://github.com/madskristensen/EditorColorPreview/blob/master/src/Adornments/ColorAdornmentTagger.cs
/// </summary>
internal abstract class ColorTaggerBase : ITagger<IntraTextAdornmentTag>, IDisposable
{
    private readonly ITextBuffer _buffer;
    private readonly ITextView _view;
    private readonly ProjectCompletionValues _completionUtilities;
    private bool _isProcessing;
    private General _generalOptions;

    protected ColorTaggerBase(ITextBuffer buffer, ITextView view, CompletionUtilities completionUtilities)
    {
        _buffer = buffer;
        _view = view;
        _completionUtilities = completionUtilities.GetCompletionConfigurationByFilePath(_buffer.GetFileName());
        _buffer.Changed += OnBufferChanged;
        General.Saved += GeneralSettingsChanged;
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        if (_isProcessing || e.Changes.Count == 0)
        {
            return;
        }

        try
        {
            _isProcessing = true;
            var start = e.Changes.First().NewSpan.Start;
            var end = e.Changes.Last().NewSpan.End;

            var startLine = e.After.GetLineFromPosition(start);
            var endLine = e.After.GetLineFromPosition(end);

            var span = new SnapshotSpan(e.After, Span.FromBounds(startLine.Start, endLine.End));
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }
        finally
        {
            _isProcessing = false;
        }
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public void Dispose()
    {
        _buffer.Changed -= OnBufferChanged;
        General.Saved -= GeneralSettingsChanged;
    }

    /// <summary>
    /// Gets the class="" / @apply scopes in the specified span.
    /// </summary>
    protected abstract IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot);

    private void GeneralSettingsChanged(General general)
    {
        _generalOptions = general;
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
    }

    private bool Enabled()
    {
        _generalOptions ??= ThreadHelper.JoinableTaskFactory.Run(General.GetLiveInstanceAsync);

        return _generalOptions.ShowColorPreviews && _generalOptions.UseTailwindCss;
    }

    public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        var tags = new List<ITagSpan<IntraTextAdornmentTag>>();

        if (!spans.Any() || !Enabled())
        {
            return tags;
        }

        foreach (var span in spans)
        {
            tags.AddRange(GetAdornments(span));
        }

        return tags;
    }

    private IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetAdornments(SnapshotSpan span)
    {
        foreach (var scope in GetScopes(span, span.Snapshot))
        {
            var color = GetRgbaFromClass(scope.GetText());
            if (color is null)
            {
                continue;
            }
            var winColor = Color.FromArgb(color[3], color[0], color[1], color[2]);
            var tag = new IntraTextAdornmentTag(new ColorAdornment(winColor, _view), null, PositionAffinity.Successor);

            yield return new TagSpan<IntraTextAdornmentTag>(new SnapshotSpan(scope.Snapshot, scope.Start, 0), tag);
        }
    }

    private byte[] GetRgbaFromClass(string text)
    {
        text = text.Split(':').Last();

        if (ImportantModifierHelper.IsImportantModifier(text))
        {
            text = text.TrimStart('!');
        }

        if (string.IsNullOrWhiteSpace(_completionUtilities.Prefix) == false)
        {
            if (text.StartsWith(_completionUtilities.Prefix))
            {
                text = text.Substring(_completionUtilities.Prefix.Length);
            }
            else if (text.StartsWith($"-{_completionUtilities.Prefix}"))
            {
                text = text.Substring(_completionUtilities.Prefix.Length + 1);
            }
            else
            {
                return null;
            }
        }

        var endsWithArbitrary = text.LastIndexOf('[');
        var segmentText = text;

        if (endsWithArbitrary != -1)
        {
            segmentText = text.Substring(0, endsWithArbitrary);
        }

        var segments = segmentText.Split([ '-' ], StringSplitOptions.RemoveEmptyEntries).ToList();

        if (endsWithArbitrary != -1)
        {
            segments.Add(text.Substring(endsWithArbitrary));
        }

        if (segments.Count >= 2)
        {
            string color;
            if (segments.Count >= 3)
            {
                color = string.Join("-", segments.Skip(1));
            }
            else
            {
                color = segments[segments.Count - 1];
            }
            var stem = text.Replace(color, "{0}");

            var opacityText = color.Split('/').Last();
            int opacity = 100;

            if (opacityText != color)
            {
                color = color.Replace($"/{opacityText}", "");
                if (int.TryParse(opacityText, out var o))
                {
                    opacity = o;
                }
            }

            if (color[0] == '[' && color[color.Length - 1] == ']')
            {
                var c = color.Substring(1, color.Length - 2);
                if (ColorHelpers.IsHex(c, out var hex))
                {
                    var fromHex = System.Drawing.ColorTranslator.FromHtml($"#{hex}");
                    return [fromHex.R, fromHex.G, fromHex.B, fromHex.A];
                }
                else if (c.StartsWith("rgb"))
                {
                    var numbers = c.Substring(c.IndexOf('(') + 1, c.IndexOf(')') - c.IndexOf('(') - 1)
                        .Split([' ', ',', '/'], StringSplitOptions.RemoveEmptyEntries);
                    if (numbers.Length == 3)
                    {
                        return [byte.Parse(numbers[0]), byte.Parse(numbers[1]), byte.Parse(numbers[2]), 255];
                    }
                    else if (numbers.Length == 4)
                    {
                        // decimal or percent
                        if (!double.TryParse(numbers[3], out var alpha))
                        {
                            alpha = double.Parse(numbers[3].Replace("%", "")) / 100;
                        }

                        return [byte.Parse(numbers[0]), byte.Parse(numbers[1]), byte.Parse(numbers[2]), (byte)(alpha * 255)];
                    }
                }
                return null;
            }

            if (_completionUtilities.DescriptionMapper.ContainsKey(stem.Replace("{0}", "{c}")) == false)
            {
                return null;
            }

            string value;
            if (_completionUtilities.CustomColorMappers != null && _completionUtilities.CustomColorMappers.ContainsKey(stem))
            {
                if (_completionUtilities.CustomColorMappers[stem].TryGetValue(color, out value) == false)
                {
                    return null;
                }
            }
            else if (_completionUtilities.ColorMapper.TryGetValue(color, out value) == false)
            {
                return null;
            }

            if (ColorHelpers.ConvertToRgb(value) is int[] converted && converted.Length == 3)
            {
                return [(byte)converted[0], (byte)converted[1], (byte)converted[2], (byte)Math.Round(opacity / 100d * 255)];
            }
            else
            {
                var rgb = value.Split(',');

                if (rgb.Length != 3)
                {
                    // inherit, current, etc.
                    return null;
                }

                return [byte.Parse(rgb[0]), byte.Parse(rgb[1]), byte.Parse(rgb[2]), (byte)Math.Round(opacity / 100d * 255)];
            }
        }
        return null;
    }
}
