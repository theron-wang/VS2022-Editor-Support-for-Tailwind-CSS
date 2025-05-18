using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace TailwindCSSIntellisense.Configuration.Descriptions;

[Export(typeof(DescriptionGenerator))]
internal class FontFamilyDescriptionGenerator : DescriptionGenerator
{
    public override string Handled { get; } = "fontFamily";

    public override string? GetDescription(object value)
    {
        if (value is string)
        {
            return $"font-family: '{value}'";
        }
        else if (value is IEnumerable<string> array)
        {
            return $"font-family: {string.Join(", ", array.Select(v => $"'{v}'"))}";
        }
        return null;
    }
}
