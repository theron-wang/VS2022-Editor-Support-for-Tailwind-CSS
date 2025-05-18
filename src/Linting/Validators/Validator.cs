using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Options;

namespace TailwindCSSIntellisense.Linting.Validators;
internal abstract class Validator : IDisposable
{
    protected readonly ITextBuffer _buffer;
    protected readonly LinterUtilities _linterUtils;
    protected readonly ProjectConfigurationManager _projectConfigurationManager;
    protected readonly ProjectCompletionValues _projectCompletionValues;

    protected readonly HashSet<SnapshotSpan> _checkedSpans = [];

    private ITextSnapshot? _snapshot;

    private readonly object _updateLock = new();

    public List<Error> Errors { get; protected set; } = [];

    /// <summary>
    /// Sends an <see cref="IEnumerable{T}"/> of type <see cref="Span"/> of changed spans or null if the entire document was revalidated
    /// </summary>
    public Action<IEnumerable<Span>?>? Validated;

    public Action<ITextBuffer>? BufferValidated;

    public Validator(ITextBuffer buffer, LinterUtilities linterUtils, ProjectConfigurationManager completionUtilities)
    {
        _buffer = buffer;
        _linterUtils = linterUtils;
        _projectConfigurationManager = completionUtilities;
        _projectCompletionValues = completionUtilities.GetCompletionConfigurationByFilePath(_buffer.GetFileName());
        _buffer.ChangedHighPriority += OnBufferChange;
        Linter.Saved += LinterOptionsChanged;
        _projectConfigurationManager.Configuration.ConfigurationUpdated += ConfigurationUpdated;

        StartUpdate();
    }

    private void OnBufferChange(object sender, TextContentChangedEventArgs e)
    {
        if (_linterUtils.LinterEnabled())
        {
            _snapshot = e.After;
            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                NormalUpdate(e);
            }).FireAndForget();
        }
    }

    private void StartUpdate()
    {
        if (_linterUtils.LinterEnabled())
        {
            ThreadHelper.JoinableTaskFactory.StartOnIdle(ForceUpdate).FireAndForget();
        }
    }

    private void ForceUpdate()
    {
        Errors.Clear();
        _checkedSpans.Clear();

        var scopes = GetScopes(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length));
        foreach (var scope in scopes)
        {
            Errors.AddRange(GetErrors(scope));
        }

        Validated?.Invoke(null);
        BufferValidated?.Invoke(_buffer);
    }

    private void NormalUpdate(TextContentChangedEventArgs e)
    {
        lock (_updateLock)
        {
            foreach (var change in e.Changes)
            {
                Errors.RemoveAll(e =>
                    e.Span.IntersectsWith(change.OldSpan) ||
                    (change.OldSpan.IsEmpty && e.Span.Contains(change.OldSpan)));
                _checkedSpans.RemoveWhere(s => s.IntersectsWith(change.OldSpan) ||
                    (change.OldSpan.IsEmpty && s.Contains(change.OldSpan)));
            }

            foreach (var error in Errors)
            {
                error.Span = error.Span.TranslateTo(e.After, SpanTrackingMode.EdgeInclusive);
            }

            foreach (var span in _checkedSpans.ToList())
            {
                _checkedSpans.Remove(span);
                _checkedSpans.Add(span.TranslateTo(e.After, SpanTrackingMode.EdgeInclusive));
            }

            if (_snapshot is not null && _snapshot != e.After)
            {
                return;
            }

            List<Span> update = [];
            foreach (var change in e.Changes)
            {
                foreach (var scope in GetScopes(new SnapshotSpan(e.After, change.NewSpan)))
                {
                    Errors.RemoveAll(err =>
                        err.Span.IntersectsWith(scope) ||
                        (scope.IsEmpty && err.Span.Contains(scope)));
                    _checkedSpans.RemoveWhere(s => s.IntersectsWith(scope) ||
                        (scope.IsEmpty && s.Contains(scope)));

                    var errors = GetErrors(scope);

                    update.Add(scope.Span);

                    Errors.AddRange(errors);
                }
            }

            Validated?.Invoke(update);
            BufferValidated?.Invoke(_buffer);
        }
    }

    public void Dispose()
    {
        _buffer.ChangedHighPriority -= OnBufferChange;
        Linter.Saved -= LinterOptionsChanged;
        _projectConfigurationManager.Configuration.ConfigurationUpdated -= ConfigurationUpdated;
    }

    private void LinterOptionsChanged(Linter linter)
    {
        StartUpdate();
    }

    private void ConfigurationUpdated()
    {
        StartUpdate();
    }

    public abstract IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span);

    public abstract IEnumerable<Error> GetErrors(SnapshotSpan span, bool force = false);
}
