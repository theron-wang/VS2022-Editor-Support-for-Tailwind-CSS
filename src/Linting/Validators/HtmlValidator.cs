﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Parsers;

namespace TailwindCSSIntellisense.Linting.Validators;
internal class HtmlValidator : HtmlLikeValidator
{
    protected override Func<string, IEnumerable<Match>> ClassSplitter { get; set; } = ClassRegexHelper.SplitNonRazorClasses;
    protected override Func<string, string, IEnumerable<Match>> ClassMatchGetter { get; set; } = ClassRegexHelper.GetClassesNormal;

    protected HtmlValidator(ITextBuffer buffer, LinterUtilities linterUtils, ProjectConfigurationManager completionUtilities) : base(buffer, linterUtils, completionUtilities)
    {

    }

    public override IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span)
    {
        return HtmlParser.GetScopes(span);
    }

    public static Validator Create(ITextBuffer buffer, LinterUtilities linterUtils, ProjectConfigurationManager completionUtilities)
    {
        return buffer.Properties.GetOrCreateSingletonProperty<Validator>(() => new HtmlValidator(buffer, linterUtils, completionUtilities));
    }
}
