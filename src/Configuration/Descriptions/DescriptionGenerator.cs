using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Configuration.Descriptions
{
    internal abstract class DescriptionGenerator
    {
        public abstract string Handled { get; }

        public abstract string GetDescription(object value);
    }
}
