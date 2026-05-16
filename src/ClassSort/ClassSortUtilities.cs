using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using TailwindCSSIntellisense.Completions;
using TailwindCSSIntellisense.Initialization;

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

        var order = ThreadHelper.JoinableTaskFactory.Run(() => ResourcesLoader.LoadOrderForVersionAsync(version));

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

        var order = ThreadHelper.JoinableTaskFactory.Run(() => ResourcesLoader.LoadOrderForVersionAsync(version, true));

        var variantToOrderIndex = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++)
        {
            // Multiply by 100 so that containers/breakpoints have flexibility
            variantToOrderIndex[order[i]] = i * 100;
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
