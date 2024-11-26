using Community.VisualStudio.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TailwindCSSIntellisense.Helpers;
internal class ClassRegexHelper
{
    // To get the match value, get capture group 2
    // https://regex101.com/r/Odcyjx/3
    private static readonly Regex _classRegex = new(@"[cC]lass\s*=\s*(['""])\s*(?<content>(?:\n.|(?!\1).)*)?\s*(\1|$)", RegexOptions.Compiled);
    private static readonly Regex _javaScriptClassRegex = new(@"[cC]lassName\s*=\s*(['""])\s*(?<content>(?:\n.|(?!\1).)*)?\s*(\1|$)", RegexOptions.Compiled);
    private static readonly Regex _razorClassRegex = new(@"[cC]lass(?:es)?\s*=\s*([""'])(?<content>(?:[^""'\\@]|\\.|@\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\)|(?:(?!\1)[^\\]|\\.)|\([^)]*\))*)(\1|$)", RegexOptions.Compiled);
    private static readonly Regex _razorSplitClassRegex = new(@"@\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\)|([^\s]+)", RegexOptions.Compiled);
    private static readonly Regex _splitClassRegex = new(@"([^\s]+)", RegexOptions.Compiled);

    private static Regex _customRegex;

    public static void SetCustomRegex(string regex)
    {
        if (string.IsNullOrWhiteSpace(regex))
        {
            _customRegex = null;
            return;
        }

        try
        {
            _customRegex = new Regex(regex, RegexOptions.Compiled);
        }
        catch (Exception ex)
        {
            ex.Log("Tailwind CSS: Setting custom regex failed.");
            VS.MessageBox.ShowError("Invalid custom regex. Please check the output window for more details.");
            _customRegex = null;
        }
    }

    /// <summary>
    /// Gets all class matches in a normal HTML context.
    /// Includes: class="...".
    /// </summary>
    public static IEnumerable<Match> GetClassesNormal(string text, string expandedSearchText)
    {
        var matches = _classRegex.Matches(text).Cast<Match>();

        return AddCustomRegexClasses(matches, expandedSearchText);
    }

    /// <summary>
    /// Gets all class matches in a razor context.
    /// Includes: class="...".
    /// </summary>
    public static IEnumerable<Match> GetClassesRazor(string text, string expandedSearchText)
    {
        var matches = _razorClassRegex.Matches(text).Cast<Match>();

        return AddCustomRegexClasses(matches, expandedSearchText);
    }

    /// <summary>
    /// Gets all class matches in a razor context.
    /// Includes: class="...". This specific method uses a yield return method.
    /// </summary>
    public static IEnumerable<Match> GetClassesRazorEnumerator(string text)
    {
        var lastMatchIndex = 0;

        foreach (var match in AddCustomRegexClassesEnumerator(text))
        {
            yield return match;
        }

        while (_razorClassRegex.Match(text, lastMatchIndex) is Match match && match.Success)
        {
            lastMatchIndex = match.Index + match.Length;
            yield return match;
        }

        yield break;
    }
    

    /// <summary>
    /// Gets all class matches in a normal context.
    /// Includes: class="...". This specific method uses a yield return method.
    /// </summary>
    public static IEnumerable<Match> GetClassesNormalEnumerator(string text)
    {
        var lastMatchIndex = 0;

        foreach (var match in AddCustomRegexClassesEnumerator(text))
        {
            yield return match;
        }

        while (_classRegex.Match(text, lastMatchIndex) is Match match && match.Success)
        {
            lastMatchIndex = match.Index + match.Length;
            yield return match;
        }

        yield break;
    }
    /// <summary>
    /// Gets all class matches in a normal context.
    /// Includes: className="...". This specific method uses a yield return method.
    /// </summary>
    public static IEnumerable<Match> GetClassesJavaScriptEnumerator(string text)
    {
        var lastMatchIndex = 0;

        foreach (var match in AddCustomRegexClassesEnumerator(text))
        {
            yield return match;
        }

        while (_javaScriptClassRegex.Match(text, lastMatchIndex) is Match match && match.Success)
        {
            lastMatchIndex = match.Index + match.Length;
            yield return match;
        }

        yield break;
    }

    /// <summary>
    /// Splits the razor class attribute into individual classes; should be called on each Match
    /// from GetClassesRazor
    /// </summary>
    /// <param name="text">An individual razor class context (the ... in class="...")</param>
    public static IEnumerable<Match> SplitRazorClasses(string text)
    {
        var matches = _razorSplitClassRegex.Matches(text).Cast<Match>();
        return matches;
    }

    /// <summary>
    /// Splits the class attribute into individual classes; should be called on each Match
    /// from GetClassesRazor
    /// </summary>
    /// <param name="text">An individual razor class context (the ... in class="...")</param>
    public static IEnumerable<Match> SplitNonRazorClasses(string text)
    {
        var matches = _splitClassRegex.Matches(text).Cast<Match>();
        return matches;
    }

    /// <summary>
    /// Gets all class matches in a JS context, such as with React.
    /// Includes: className="...".
    /// </summary>
    public static IEnumerable<Match> GetClassesJavaScript(string text, string expandedSearchText)
    {
        var matches = _javaScriptClassRegex.Matches(text).Cast<Match>();

        return AddCustomRegexClasses(matches, expandedSearchText);
    }

    public static Group GetClassTextGroup(Match match)
    {
        // 'content' capture group matches the class value
        return match.Groups["content"];
    }

    private static IEnumerable<Match> AddCustomRegexClassesEnumerator(string text)
    {
        if (_customRegex is not null)
        {
            var lastMatchIndex = 0;

            while (_customRegex.Match(text, lastMatchIndex) is Match match && match.Success)
            {
                lastMatchIndex = match.Index + match.Length;
                yield return match;
            }
        }

        yield break;
    }

    private static IEnumerable<Match> AddCustomRegexClasses(IEnumerable<Match> original, string text)
    {
        if (_customRegex is not null)
        {
            return _customRegex.Matches(text).Cast<Match>().Concat(original);
        }

        return original;
    }
}
