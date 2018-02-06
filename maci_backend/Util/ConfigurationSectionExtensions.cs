using System;
using Microsoft.Extensions.Configuration;

namespace Backend.Util
{
    public static class ConfigurationSectionExtensions
    {
        /// <summary>
        ///     Retrieves a setting from a configuration section and throws if it doesn't exist.
        /// </summary>
        public static string GetRequiredSetting(this IConfigurationSection section, string path)
        {
            var setting = section[path];
            if (setting == null)
            {
                throw new ArgumentException($"Configuration is faulty: Expected property '{path}'.");
            }
            return setting;
        }

	public static string GetSetting(this IConfigurationSection section, string path, string defaultValue)
        {
            var setting = section[path];
            if (setting == null)
            {
                return defaultValue;
            }
            return setting;
        }
    }
}
