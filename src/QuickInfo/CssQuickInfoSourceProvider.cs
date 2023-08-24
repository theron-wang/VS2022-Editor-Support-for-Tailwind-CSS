using Microsoft.VisualStudio.Language.Intellisense;
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
    [Name("CSS Async Quick Info Provider")]
    [ContentType("css")]
    internal sealed class CssQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        public CompletionUtilities CompletionUtilities { get; set; }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new CssQuickInfoSource(textBuffer, CompletionUtilities));
        }
    }
}
