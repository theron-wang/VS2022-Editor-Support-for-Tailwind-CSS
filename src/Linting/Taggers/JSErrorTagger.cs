﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Linting.Validators;

namespace TailwindCSSIntellisense.Linting.Taggers;

[Export(typeof(ITaggerProvider))]
[TagType(typeof(IErrorTag))]
[ContentType("JavaScript")]
[ContentType("TypeScript")]
[ContentType("jsx")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TextViewRole(PredefinedTextViewRoles.Analyzable)]
internal class JSErrorTaggerProvider : ITaggerProvider
{
    [Import]
    public LinterUtilities LinterUtilities { get; set; }
    [Import]
    public CompletionUtilities CompletionUtilities { get; set; }

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
        return buffer.Properties.GetOrCreateSingletonProperty(() => new JSErrorTagger(buffer, LinterUtilities, CompletionUtilities)) as ITagger<T>;
    }

    internal sealed class JSErrorTagger : ErrorTaggerBase, IDisposable
    {
        public JSErrorTagger(ITextBuffer buffer, LinterUtilities linterUtils, CompletionUtilities completionUtilities) : base(buffer, linterUtils)
        {
            _errorChecker = JSValidator.Create(buffer, linterUtils, completionUtilities);
            _errorChecker.Validated += UpdateErrors;
        }

        public void Dispose()
        {
            _errorChecker.Validated -= UpdateErrors;
        }
    }
}