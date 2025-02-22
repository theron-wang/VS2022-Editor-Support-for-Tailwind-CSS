using System.Collections.Generic;
using System.Collections.Immutable;

namespace TailwindCSSIntellisense.Helpers;
internal static class BgGradientModifiers
{
    public static readonly IReadOnlyDictionary<string, string> ModifiersToDescriptions = new Dictionary<string, string>()
    {
        { "oklab", "oklab" },
        { "oklch", "oklch" },
        { "srgb", "srgb" },
        { "hsl", "hsl" },
        { "longer", "oklch longer hue" },
        { "shorter", "oklch shorter hue" },
        { "increasing", "oklch increasing hue" },
        { "decreasing", "oklch decreasing hue" }
    }.ToImmutableDictionary();
}
