using System;
using System.Collections.Generic;

namespace TailwindCSSIntellisense.Configuration
{
    internal class TailwindConfiguration
    {
        /// <summary>
        /// Corresponds to theme.____. This value will NEVER be null.
        /// </summary>
        /// <remarks>
        /// The key is the class being overriden, such as color. <br></br>
        /// The value is a <see cref="Dictionary{TKey, TValue}"/>, which holds the specific key/value pairs of the overriden values.
        /// </remarks>
        public Dictionary<string, object> OverridenValues { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Corresponds to theme.extend.____. This value will NEVER be null.
        /// </summary>
        /// <remarks>
        /// The key is the class being extended, such as color. <br></br>
        /// The value is a <see cref="Dictionary{TKey, TValue}"/>, which holds the specific key/value pairs of the extended values.
        /// </remarks>
        public Dictionary<string, object> ExtendedValues { get; set; } = new Dictionary<string, object>();

        public string Prefix { get; set; }

        public List<string> PluginClasses { get; set; }

        public List<string> PluginModifiers { get; set; }
    }
}
