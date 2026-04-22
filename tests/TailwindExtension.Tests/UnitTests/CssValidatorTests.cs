using System.Reflection;
using System.Runtime.Serialization;
using TailwindCSSIntellisense.Linting.Validators;

namespace TailwindExtension.Tests.UnitTests;

public class CssValidatorTests
{
    [Theory]
    [InlineData("colors.blue.500", new[] { "colors", "blue", "500" })]
    [InlineData("spacing[2.5].value", new[] { "spacing", "2.5", "value" })]
    [InlineData("fontSize.sm", new[] { "fontSize", "sm" })]
    public void TokenizeTheme_SplitsDotNotationAndBracketSegments(string input, string[] expected)
    {
        var validator = CreateUninitializedValidator();

        var tokens = InvokePrivate<List<string>>(validator, "TokenizeTheme", input);

        Assert.Equal(expected, tokens);
    }

    [Theory]
    [InlineData("@tailwind utilities;", "@tailwind", true)]
    [InlineData("@tailwind base; @tailwind utilities;", "@tailwind", false)]
    [InlineData("@media screen(sm){}", "@screen", false)]
    public void HasOnlyOneDirective_DetectsSingleOccurrence(string text, string directive, bool expected)
    {
        var validator = CreateUninitializedValidator();

        var result = InvokePrivate<bool>(validator, "HasOnlyOneDirective", text, directive);

        Assert.Equal(expected, result);
    }

    private static CssValidator CreateUninitializedValidator()
    {
#pragma warning disable SYSLIB0050
        return (CssValidator)FormatterServices.GetUninitializedObject(typeof(CssValidator));
#pragma warning restore SYSLIB0050
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(instance, args);
        Assert.NotNull(result);
        return (T)result!;
    }
}
