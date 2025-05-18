using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace TailwindCSSIntellisense.Configuration.Descriptions;

[Export(typeof(GeneratorAggregator))]
[method: ImportingConstructor]
internal class GeneratorAggregator([ImportMany] IEnumerable<DescriptionGenerator> generators)
{
    private readonly IEnumerable<DescriptionGenerator> _generators = generators;

    public bool Handled(string attribute)
    {
        return _generators.Any(g => g.Handled == attribute);
    }

    public string? GenerateDescription(string attribute, object input)
    {
        return _generators.First(g => g.Handled == attribute).GetDescription(input);
    }
}
