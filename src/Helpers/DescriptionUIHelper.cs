using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TailwindCSSIntellisense.Helpers;
internal static class DescriptionUIHelper
{
    private static string CssEscape(string input)
    {
        string pattern = "(['\"{}()\\[\\]:;,.<>+*~?\\s!@#$%^&*()])";

        string escapedString = Regex.Replace(input, pattern, "\\$1");

        return escapedString;
    }

    /// <summary>
    /// Returns a formatted UI element for a class description (for Visual Studio)
    /// </summary>
    /// <param name="fullClass">The full class, including all modifiers</param>
    /// <param name="modifierTotal">The full modifier adjustment, i.e. &[open]:hover</param>
    /// <param name="mediaQueries">A list of media queries</param>
    /// <param name="desc">The description, already formatted via DescriptionGenerator</param>
    /// <param name="isImportant">!important or not</param>
    /// <returns></returns>
    internal static ContainerElement GetDescriptionAsUIFormatted(string fullClass, string modifierTotal, string[] mediaQueries, string desc, bool isImportant)
    {
        modifierTotal ??= "&";
        var mediaQueryElements = new List<ClassifiedTextElement>();

        const string singleIndent = "  ";
        string totalIndent = "";

        foreach (var mediaQuery in mediaQueries)
        {
            var query = mediaQuery.Split()[0];
            var openParen = mediaQuery.IndexOf('(', query.Length);

            var text = new List<ClassifiedTextRun>
            {
                new(PredefinedClassificationTypeNames.WhiteSpace, totalIndent, ClassifiedTextRunStyle.UseClassificationFont),
                new(PredefinedClassificationTypeNames.Type, query, ClassifiedTextRunStyle.UseClassificationFont),
                new(PredefinedClassificationTypeNames.WhiteSpace, " ", ClassifiedTextRunStyle.UseClassificationFont)
            };

            if (openParen == -1)
            {
                text.Add(new(PredefinedClassificationTypeNames.Identifier, mediaQuery.Substring(query.Length).Trim() + " {", ClassifiedTextRunStyle.UseClassificationFont));
                mediaQueryElements.Add(new ClassifiedTextElement(text));

                totalIndent += singleIndent;
                continue;
            }

            var intermediate = mediaQuery.Substring(query.Length, openParen - query.Length).Trim();
            var closeParen = mediaQuery.LastIndexOf(')');

            var inner = mediaQuery.Substring(openParen + 1, closeParen - openParen - 1).Trim();

            var colon = inner.IndexOf(':');

            if (!string.IsNullOrWhiteSpace(intermediate))
            {
                text.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, $"{intermediate} ", ClassifiedTextRunStyle.UseClassificationFont));
            }

            text.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "(", ClassifiedTextRunStyle.UseClassificationFont));

            if (colon == -1)
            {
                text.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupNode, inner, ClassifiedTextRunStyle.UseClassificationFont));
            }
            else
            {
                var first = inner.Substring(0, colon + 1);
                var second = inner.Substring(colon + 1);

                text.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttribute, first, ClassifiedTextRunStyle.UseClassificationFont));
                text.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttributeValue, second, ClassifiedTextRunStyle.UseClassificationFont));
            }

            text.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ") {", ClassifiedTextRunStyle.UseClassificationFont));

            mediaQueryElements.Add(new ClassifiedTextElement(text));

            totalIndent += singleIndent;
        }

        var mediaQueryContainerElement = new ContainerElement(ContainerElementStyle.Stacked, mediaQueryElements);

        var classElement = new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Literal, $"{totalIndent}{modifierTotal.Replace("&", $".{CssEscape(fullClass)}")} {{", ClassifiedTextRunStyle.UseClassificationFont)
                    ));

        totalIndent += singleIndent;

        var descriptionLines = new List<ClassifiedTextElement>();

        foreach (var l in desc.Split('\n'))
        {
            var line = l.Trim();

            var keyword = line.Contains(":") ? line.Substring(0, line.IndexOf(':')).Trim() : line;
            var value = line.Substring(line.IndexOf(':') + 1).Trim().Trim(';');

            descriptionLines.Add(
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.WhiteSpace, totalIndent, ClassifiedTextRunStyle.UseClassificationFont),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttribute, keyword, ClassifiedTextRunStyle.UseClassificationFont),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ": ", ClassifiedTextRunStyle.UseClassificationFont),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.MarkupAttributeValue, value + (isImportant ? " !important" : ""), ClassifiedTextRunStyle.UseClassificationFont),
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, ";", ClassifiedTextRunStyle.UseClassificationFont)
                )
            );
        }

        var descriptionElement = new ContainerElement(
            ContainerElementStyle.Stacked,
            descriptionLines);

        var closingBrackets = new List<ClassifiedTextElement>();

        while (totalIndent.Length > 0)
        {
            totalIndent = totalIndent.Substring(2);

            closingBrackets.Add(new ClassifiedTextElement(
                new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, totalIndent + "}", ClassifiedTextRunStyle.UseClassificationFont)
            ));
        }

        var closingBracket = new ContainerElement(
            ContainerElementStyle.Stacked, closingBrackets
        );

        return new ContainerElement(
                        ContainerElementStyle.Stacked,
                        mediaQueryContainerElement,
                        classElement,
                        descriptionElement,
                        closingBracket);
    }

    /// <summary>
    /// Returns a formatted UI element for a class description (for WPF)
    /// </summary>
    /// <param name="fullClass">The full class, including all modifiers</param>
    /// <param name="desc">The description, already formatted via DescriptionGenerator</param>
    /// <param name="isImportant">!important or not</param>
    /// <returns></returns>
    internal static UIElement GetDescriptionAsWPFFormatted(string fullClass, string desc, bool isImportant)
    {
        var classElement = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        classElement.Children.Add(new TextBlock
        {
            Text = $".{CssEscape(fullClass)} {{",
            FontFamily = new FontFamily("Consolas"),
            Foreground = (Brush)Application.Current.Resources[VsBrushes.GrayTextKey]
        });

        var descriptionPanel = new StackPanel();
        foreach (var l in desc.Split('\n'))
        {
            var line = l.Trim();

            var keyword = line.Contains(":") ? line.Substring(0, line.IndexOf(':')).Trim() : line;
            var value = line.Substring(line.IndexOf(':') + 1).Trim().Trim(';');

            var linePanel = new StackPanel { Orientation = Orientation.Horizontal };

            linePanel.Children.Add(new TextBlock
            {
                Text = "  ",
                FontFamily = new FontFamily("Consolas")
            });
            linePanel.Children.Add(new TextBlock
            {
                Text = keyword,
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)Application.Current.Resources[VsBrushes.VizSurfaceStrongBlueMediumKey]
            });
            linePanel.Children.Add(new TextBlock
            {
                Text = ": ",
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)Application.Current.Resources[VsBrushes.GrayTextKey]
            });
            linePanel.Children.Add(new TextBlock
            {
                Text = value + (isImportant ? " !important" : ""),
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)Application.Current.Resources[VsBrushes.GrayTextKey]
            });
            linePanel.Children.Add(new TextBlock
            {
                Text = ";",
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)Application.Current.Resources[VsBrushes.GrayTextKey]
            });

            descriptionPanel.Children.Add(linePanel);
        }

        var closingBracket = new TextBlock
        {
            Text = "}",
            FontFamily = new FontFamily("Consolas"),
            Foreground = (Brush)Application.Current.Resources[VsBrushes.GrayTextKey]
        };

        var containerPanel = new StackPanel();
        containerPanel.Children.Add(classElement);
        containerPanel.Children.Add(descriptionPanel);
        containerPanel.Children.Add(closingBracket);

        return containerPanel;

    }
}
