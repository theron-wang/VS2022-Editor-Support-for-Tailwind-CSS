using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.Helpers;

[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal class ColorIconGenerator
{
    [Import]
    internal CompletionUtilities CompletionUtilities { get; set; }

    private readonly Dictionary<string, ImageSource> _colorToRgbMapperCache = [];

    internal void ClearCache()
    {
        _colorToRgbMapperCache.Clear();
    }

    internal ImageSource GetImageFromColor(string stem, string color, int opacity = 100)
    {
        if (_colorToRgbMapperCache.TryGetValue($"{stem}/{color}/{opacity}", out var result) || _colorToRgbMapperCache.TryGetValue($"{color}/{opacity}", out result))
        {
            return result;
        }

        string value;
        if (CompletionUtilities.CustomColorMappers != null)
        {
            if (CompletionUtilities.CustomColorMappers.TryGetValue(stem, out var dict) == false || (dict.TryGetValue(color, out value) && CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out var value2) && value == value2))
            {
                if (_colorToRgbMapperCache.TryGetValue($"{color}/{opacity}", out result))
                {
                    return result;
                }

                if (CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
                {
                    return CompletionUtilities.TailwindLogo;
                }
            }
        }
        else if (CompletionUtilities.ColorToRgbMapper.TryGetValue(color, out value) == false)
        {
            return CompletionUtilities.TailwindLogo;
        }

        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("{noparse}"))
        {
            return CompletionUtilities.TailwindLogo;
        }

        var rgb = value.Split(',');

        if (rgb.Length == 0)
        {
            // Something wrong happened: fall back to default tailwind icon
            return CompletionUtilities.TailwindLogo;
        }
        var r = byte.Parse(rgb[0]);
        var g = byte.Parse(rgb[1]);
        var b = byte.Parse(rgb[2]);
        var a = (byte)Math.Round(opacity / 100d * 255);

        var pen = new Pen() { Thickness = 8, Brush = new SolidColorBrush(Color.FromArgb(a, r, g, b)) };
        var mainImage = new GeometryDrawing() { Geometry = new RectangleGeometry(new Rect(4, 5, 9, 8)), Pen = pen };

        // https://stackoverflow.com/questions/37663993/preventing-icon-color-and-size-distortions-when-bundling-a-visual-studio-project
        var pen2 = new Pen() { Thickness = 1, Brush = new SolidColorBrush(Color.FromArgb(1, 0, 255, 255)) };
        var vsPrevent = new GeometryDrawing() { Geometry = new RectangleGeometry(new Rect(16, 0, 1, 1)), Pen = pen2 };

        var geometry = new DrawingGroup();
        geometry.Children.Add(mainImage);
        geometry.Children.Add(vsPrevent);

        result = new DrawingImage
        {
            Drawing = geometry
        };

        var key = $"{color}/{opacity}";
        if (string.IsNullOrEmpty(stem) == false)
        {
            key = $"{stem}/{key}";
        }

        _colorToRgbMapperCache[key] = result;

        return result;
    }
}
