using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Completions
{
    internal class TailwindClass
    {
        public string Name { get; set; }

        public bool UseColors { get; set; }

        public bool UseSpacing { get; set; }

        public bool SupportsBrackets { get; set; }
    }
}
