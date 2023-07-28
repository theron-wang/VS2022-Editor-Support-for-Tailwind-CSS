using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions.Sources;
using TailwindCSSIntellisense.Settings;

namespace TailwindCSSIntellisense.Completions.Providers
{
    /// <summary>
    /// A factory for creating <see cref="HtmlCompletionSource"/>; exported via MEF.
    /// </summary>
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("razor")]
    [ContentType("LegacyRazorCSharp")]
    [Name("TailwindCSS Razor Token Completion")]
    internal class RazorCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal CompletionUtilities CompletionUtils { get; set; }
        [Import]
        internal SettingsProvider SettingsProvider { get; set; }
        [Import]
        internal IAsyncCompletionBroker AsyncCompletionBroker { get; set; }
        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new RazorCompletionSource(CompletionUtils, SettingsProvider, AsyncCompletionBroker, CompletionBroker, textBuffer);
        }
    }
}
