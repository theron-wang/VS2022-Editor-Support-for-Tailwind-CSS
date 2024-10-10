using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TailwindCSSIntellisense.Parsers;
internal static class JSParser
{
    public static IEnumerable<SnapshotSpan> GetScopes(SnapshotSpan span, ITextSnapshot snapshot)
    {
        return HtmlParser.GetScopesImpl(span, snapshot, "className=");
    }
}
