using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.ClassSort;
[Export]
internal sealed class ClassSortUtilities
{
    private bool _initialized;

    public Dictionary<string, int> ClassOrder { get; private set; }
    public Dictionary<string, int> ModifierOrder { get; private set; }

    public async Task InitializeAsync()
    {
        if (!_initialized)
        {
            var baseFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources");

            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindorder.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var classOrder = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                ClassOrder = [];
                for (int i = 0; i < classOrder.Count; i++)
                {
                    ClassOrder[classOrder[i]] = i;
                }
            }
            using (var fs = File.Open(Path.Combine(baseFolder, "tailwindmodifiersorder.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var modifierOrder = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                ModifierOrder = [];
                for (int i = 0; i < modifierOrder.Count; i++)
                {
                    ModifierOrder[modifierOrder[i]] = i;
                }
            }

            _initialized = true;
        }
    }
}
