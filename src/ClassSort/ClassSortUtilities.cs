using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text.Json;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.ClassSort;

[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class ClassSortUtilities
{
    private readonly Dictionary<TailwindVersion, Dictionary<string, int>> _classOrders = [];
    private readonly Dictionary<TailwindVersion, Dictionary<string, int>> _variantOrders = [];

    private void InitializeClassOrder(TailwindVersion version)
    {
        if (_classOrders.ContainsKey(version))
        {
            return;
        }

        var folder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", version.ToString());

        List<string> order;
        using (var fs = File.Open(Path.Combine(folder, "order.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            order = JsonSerializer.Deserialize<List<string>>(fs)!;
        }

        var classToOrderIndex = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++)
        {
            classToOrderIndex[order[i]] = i;
        }

        _classOrders[version] = classToOrderIndex;
    }

    private void InitializeVariantOrder(TailwindVersion version)
    {
        if (_variantOrders.ContainsKey(version))
        {
            return;
        }

        var folder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", version.ToString());

        List<string> order;
        using (var fs = File.Open(Path.Combine(folder, "variantorder.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            order = JsonSerializer.Deserialize<List<string>>(fs)!;
        }

        var variantToOrderIndex = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++)
        {
            variantToOrderIndex[order[i]] = i;
        }

        _variantOrders[version] = variantToOrderIndex;
    }

    public Dictionary<string, int> GetClassOrder(ProjectCompletionValues project)
    {
        if (_classOrders.TryGetValue(project.Version, out var classOrder))
        {
            return classOrder;
        }
        else
        {
            InitializeClassOrder(project.Version);
            return _classOrders[project.Version];
        }
    }

    public Dictionary<string, int> GetVariantOrder(ProjectCompletionValues project)
    {
        if (_variantOrders.TryGetValue(project.Version, out var classOrder))
        {
            return classOrder;
        }
        else
        {
            InitializeVariantOrder(project.Version);
            return _variantOrders[project.Version];
        }
    }
}
