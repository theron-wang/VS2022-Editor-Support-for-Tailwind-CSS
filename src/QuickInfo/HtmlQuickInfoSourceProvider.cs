﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("HTML Async Quick Info Provider")]
    [ContentType("html")]
    [ContentType("WebForms")]
    internal sealed class HtmlQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        public CompletionUtilities CompletionUtilities { get; set; }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new HtmlQuickInfoSource(textBuffer, CompletionUtilities));
        }
    }
}
