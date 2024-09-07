using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace TailwindCSSIntellisense.Configuration.Descriptions
{
    [Export(typeof(GeneratorAggregator))]
    internal class GeneratorAggregator
    {
        private readonly IEnumerable<DescriptionGenerator> _generators;

        [ImportingConstructor]
        public GeneratorAggregator([ImportMany] IEnumerable<DescriptionGenerator> generators)
        {
            _generators = generators;
        }

        public bool Handled(string attribute)
        {
            return _generators.Any(g => g.Handled == attribute);
        }

        public string GenerateDescription(string attribute, object input)
        {
            return _generators.First(g => g.Handled == attribute).GetDescription(input);
        }
    }
}
