using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TailwindCSSIntellisense.Configuration
{
    internal class TailwindConfiguration
    {
        /// <summary>
        /// Corresponds to theme.____
        /// </summary>
        /// <remarks>
        /// The key is the class being overriden, such as color. <br></br>
        /// The value is a <see cref="Dictionary{TKey, TValue}"/>, which holds the specific key/value pairs of the overriden values.
        /// </remarks>
        public Dictionary<string, object> OverridenValues { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Corresponds to theme.extend.____
        /// </summary>
        /// <remarks>
        /// The key is the class being extended, such as color. <br></br>
        /// The value is a <see cref="Dictionary{TKey, TValue}"/>, which holds the specific key/value pairs of the extended values.
        /// </remarks>
        public Dictionary<string, object> ExtendedValues { get; set; } = new Dictionary<string, object>();
    }
}
