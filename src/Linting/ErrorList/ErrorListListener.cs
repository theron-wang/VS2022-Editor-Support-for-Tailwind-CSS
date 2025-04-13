﻿using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Linting.Validators;

namespace TailwindCSSIntellisense.Linting.ErrorList;
internal abstract class ErrorListListener : ITextViewCreationListener, IDisposable
{
    protected abstract Validator GetValidator(ITextView view);

    [Import]
    protected LinterUtilities _linterUtilities = null;
    [Import]
    protected ProjectConfigurationManager _projectConfigurationManager = null;

    private readonly Dictionary<ITextBuffer, ErrorListContext> _contexts = [];

    public virtual void TextViewCreated(ITextView view)
    {
        if (_contexts.ContainsKey(view.TextBuffer) == false)
        {
            view.Closed += ViewClosed;

            var file = view.TextBuffer.GetFileName();

            if (file is null)
            {
                return;
            }

            var tableDataSource = new TableDataSource(Vsix.Name + file);

            var project = ThreadHelper.JoinableTaskFactory.Run(() => PhysicalFile.FromFileAsync(file))?.ContainingProject;

            if (project is null)
            {
                return;
            }

            var validator = GetValidator(view);
            validator.BufferValidated += UpdateErrorList;

            _contexts[view.TextBuffer] = new()
            {
                File = file,
                Project = project,
                TableDataSource = tableDataSource,
                Validator = validator
            };
        }
    }

    private void UpdateErrorList(ITextBuffer buffer)
    {
        if (_contexts.TryGetValue(buffer, out var context))
        {
            var errorListItems = context.Validator.Errors.Where(e =>
                    _linterUtilities.GetErrorSeverity(e.ErrorType) != ErrorSeverity.None)
                .Select(e =>
                {
                    var line = e.Span.Snapshot.GetLineFromPosition(e.Span.Start);
                    var severity = _linterUtilities.GetErrorSeverity(e.ErrorType);

                    return new ErrorListItem()
                    {
                        BuildTool = Vsix.Name,
                        Column = e.Span.Start.Position - line.Start.Position,
                        ErrorCategory = _linterUtilities.GetErrorTagFromSeverity(severity),
                        FileName = context.File,
                        Icon = GetIconByErrorType(severity),
                        Line = line.LineNumber,
                        Message = e.ErrorMessage,
                        ProjectName = context.Project.Name,
                        Severity = GetErrorCategory(severity),
                        ErrorCode = e.ErrorType.ToString()
                    };
                }
            );

            context.TableDataSource.CleanAllErrors();
            context.TableDataSource.AddErrors(errorListItems);
        }
    }

    private void ViewClosed(object sender, EventArgs e)
    {
        var view = (IWpfTextView)sender;
        view.Closed -= ViewClosed;

        if (_contexts.TryGetValue(view.TextBuffer, out var context))
        {
            context.Validator.BufferValidated -= UpdateErrorList;

            context.TableDataSource.CleanAllErrors();
        }
    }

    private ImageMoniker GetIconByErrorType(ErrorSeverity severity)
    {
        if (severity == ErrorSeverity.Warning)
        {
            return KnownMonikers.StatusWarning;
        }
        else if (severity == ErrorSeverity.Error)
        {
            return KnownMonikers.StatusError;
        }

        return KnownMonikers.StatusInformation;
    }

    private __VSERRORCATEGORY GetErrorCategory(ErrorSeverity severity)
    {
        if (severity == ErrorSeverity.Warning)
        {
            return __VSERRORCATEGORY.EC_WARNING;
        }
        else if (severity == ErrorSeverity.Error)
        {
            return __VSERRORCATEGORY.EC_ERROR;
        }

        return __VSERRORCATEGORY.EC_MESSAGE;
    }

    public void Dispose()
    {
        foreach (var context in _contexts.Values)
        {
            context.Validator.BufferValidated -= UpdateErrorList;

            context.TableDataSource.CleanAllErrors();
        }
    }
}
