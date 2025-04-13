﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Linting.Validators;
internal class JSValidator : HtmlLikeValidator
{
    protected override Func<string, IEnumerable<Match>> ClassSplitter { get; set; } = ClassRegexHelper.SplitNonRazorClasses;
    protected override Func<string, string, IEnumerable<Match>> ClassMatchGetter { get; set; } = ClassRegexHelper.GetClassesJavaScript;

    protected JSValidator(ITextBuffer buffer, LinterUtilities linterUtils, ProjectConfigurationManager completionUtilities) : base(buffer, linterUtils, completionUtilities)
    {

    }

    public override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span)
    {
        return JSParser.GetScopes(span);
    }

    public static Validator Create(ITextBuffer buffer, LinterUtilities linterUtils, ProjectConfigurationManager completionUtilities)
    {
        return buffer.Properties.GetOrCreateSingletonProperty<Validator>(() => new JSValidator(buffer, linterUtils, completionUtilities));
    }
}
