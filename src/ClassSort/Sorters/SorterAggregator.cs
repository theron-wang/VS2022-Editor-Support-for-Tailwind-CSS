using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace TailwindCSSIntellisense.ClassSort.Sorters;
[Export(typeof(SorterAggregator))]
[method: ImportingConstructor]
internal class SorterAggregator([ImportMany] IEnumerable<Sorter> sorters)
{
    private readonly IEnumerable<Sorter> _sorters = sorters;

    public IEnumerable<string> AllHandled => _sorters.SelectMany(s => s.Handled);

    public bool Handled(string file)
    {
        return _sorters.Any(g => g.Handled.Contains(Path.GetExtension(file)));
    }

    public string Sort(string filePath, string fileContent)
    {
        return _sorters.First(g => g.Handled.Contains(Path.GetExtension(filePath))).Sort(filePath, fileContent);
    }
}