using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
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
    /// <param name="desc">The description, already formatted via DescriptionGenerator</param>
    /// <param name="isImportant">!important or not</param>
    /// <returns></returns>
    internal static ContainerElement GetDescriptionAsUIFormatted(string fullClass, string desc, bool isImportant)
    {
        var classElement = new ContainerElement(
                    ContainerElementStyle.Wrapped,
                    new ClassifiedTextElement(
                        new ClassifiedTextRun(PredefinedClassificationTypeNames.Literal, $".{CssEscape(fullClass)} {{", ClassifiedTextRunStyle.UseClassificationFont)
                    ));

        var descriptionLines = new List<ClassifiedTextElement>();

        foreach (var l in desc.Split('\n'))
        {
            var line = l.Trim();

            var keyword = line.Contains(":") ? line.Substring(0, line.IndexOf(':')).Trim() : line;
            var value = line.Substring(line.IndexOf(':') + 1).Trim().Trim(';');

            descriptionLines.Add(
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.WhiteSpace, "  ", ClassifiedTextRunStyle.UseClassificationFont),
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

        var closingBracket = new ContainerElement(
            ContainerElementStyle.Wrapped,
        new ClassifiedTextElement(
                new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier, "}", ClassifiedTextRunStyle.UseClassificationFont)
            )
        );

        return new ContainerElement(
                        ContainerElementStyle.Stacked,
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
