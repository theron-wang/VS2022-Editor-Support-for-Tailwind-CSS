using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using TailwindCSSIntellisense.Completions;

namespace TailwindCSSIntellisense.ClassSort;
[Export]
internal sealed class ClassSortUtilities
{
    private bool _initialized;

    private Dictionary<string, int> _classOrder = [];
    private Dictionary<string, int> _variantOrder = [];
    private Dictionary<string, int> _classOrderV4 = [];
    private Dictionary<string, int> _variantOrderV4 = [];

    public async Task InitializeAsync()
    {
        if (!_initialized)
        {
            var baseFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");
            var v4Folder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "V4");

            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindorder.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var classOrder = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                _classOrder = [];
                for (int i = 0; i < classOrder.Count; i++)
                {
                    _classOrder[classOrder[i]] = i;
                }
            }
            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindmodifiersorder.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var modifierOrder = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                _variantOrder = [];
                for (int i = 0; i < modifierOrder.Count; i++)
                {
                    _variantOrder[modifierOrder[i]] = i;
                }
            }
            using (var fs = File.Open(Path.Combine(v4Folder, "order.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var classOrder = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                _classOrderV4 = [];
                for (int i = 0; i < classOrder.Count; i++)
                {
                    _classOrderV4[classOrder[i]] = i;
                }
            }
            using (var fs = File.Open(Path.Combine(v4Folder, "variantorder.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var modifierOrder = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                _variantOrderV4 = [];
                for (int i = 0; i < modifierOrder.Count; i++)
                {
                    _variantOrderV4[modifierOrder[i]] = i;
                }
            }

            _initialized = true;
        }
    }

    public Dictionary<string, int> GetClassOrder(ProjectCompletionValues project)
    {
        if (project.Version == TailwindVersion.V4)
        {
            return _classOrderV4;
        }
        return _classOrder;
    }

    public Dictionary<string, int> GetVariantOrder(ProjectCompletionValues project)
    {
        if (project.Version == TailwindVersion.V4)
        {
            return _variantOrderV4;
        }
        return _variantOrder;
    }
}
