﻿using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
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
    [ContentType("LegacyRazor")]
    [ContentType("LegacyRazorCoreCSharp")]
    [Name("TailwindCSS Razor Token Completion")]
    [Order(After = Priority.Default, Before = Priority.High)]
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
        [Import]
        internal DescriptionGenerator DescriptionGenerator { get; set; }
        [Import]
        internal ColorIconGenerator ColorIconGenerator { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new RazorCompletionSource(textBuffer, CompletionUtils, ColorIconGenerator, DescriptionGenerator, SettingsProvider, AsyncCompletionBroker, CompletionBroker);
        }
    }
}
