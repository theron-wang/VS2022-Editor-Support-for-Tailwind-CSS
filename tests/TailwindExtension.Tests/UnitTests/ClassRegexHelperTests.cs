using System.Linq;
using TailwindCSSIntellisense.Helpers;
using TailwindCSSIntellisense.Settings;

namespace TailwindExtension.Tests.UnitTests;

public class ClassRegexHelperTests
{
    public ClassRegexHelperTests()
    {
        ClassRegexHelper.GetTailwindSettings = null;
    }

    [Fact]
    public void GetClassesNormal_FindsClassAttributes()
    {
        const string html = "<div class=\"px-4 text-red-500\"></div><span class='font-bold'></span>";

        var matches = ClassRegexHelper.GetClassesNormal(html, html).ToList();

        Assert.Equal(2, matches.Count);
        Assert.Equal("px-4 text-red-500", ClassRegexHelper.GetClassTextGroup(matches[0]).Value);
        Assert.Equal("font-bold", ClassRegexHelper.GetClassTextGroup(matches[1]).Value);
    }

    [Fact]
    public void GetClassesNormal_SplitsQuotedConditionalSegments()
    {
        const string html = "<div class=\"open ? 'px-2 text-red-500' : 'py-1 text-blue-500'\"></div>";

        var matches = ClassRegexHelper.GetClassesNormal(html, html)
            .Select(m => ClassRegexHelper.GetClassTextGroup(m).Value)
            .ToList();

        Assert.Equal(2, matches.Count);
        Assert.Contains("px-2 text-red-500", matches);
        Assert.Contains("py-1 text-blue-500", matches);
    }

    [Fact]
    public void GetClassesJavaScript_HandlesLargeMixedInput()
    {
        var input = new string('x', 6000) + "\n<div className=\"bg-blue-500 md:hover:text-white\"></div>";

        var matches = ClassRegexHelper.GetClassesJavaScript(input, input).ToList();

        Assert.Single(matches);
        Assert.Equal("bg-blue-500 md:hover:text-white", ClassRegexHelper.GetClassTextGroup(matches[0]).Value);
    }

    [Fact]
    public void CustomRegexOverride_UsesConfiguredRegex()
    {
        ClassRegexHelper.GetTailwindSettings = () => Task.FromResult(new TailwindSettings
        {
            CustomRegexes = new CustomRegexes
            {
                HTML = new CustomRegexes.CustomRegex
                {
                    Override = true,
                    Values = ["tw\\s*=\\s*\"(?<content>[^\"]+)\""]
                }
            }
        });

        const string html = "<div tw=\"p-4 text-sm\" class=\"ignored\"></div>";

        var matches = ClassRegexHelper.GetClassesNormal(html, html).ToList();

        Assert.Single(matches);
        Assert.Equal("p-4 text-sm", ClassRegexHelper.GetClassTextGroup(matches[0]).Value);
    }
}
