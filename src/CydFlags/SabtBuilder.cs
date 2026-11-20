using System;
using SimHub.Plugins;
using System.Globalization;

namespace CydFlags
{
    /// <summary>
    /// Reads the SABT (Simple Active Belt Tensioner) SimHub properties.
    /// SABT exposes tension values already divided by 10, so they are shown
    /// as-is. Returns an empty string when SABT is not installed/running.
    /// </summary>
    internal static class SabtBuilder
    {
        public static string Build(PluginManager pluginManager)
        {
            if (pluginManager == null) return string.Empty;

            object enabled = Read(pluginManager, "SABT.IsEnabled");
            object idle = Read(pluginManager, "SABT.IdleTension");
            object resting = Read(pluginManager, "SABT.RestingTension");
            object maximum = Read(pluginManager, "SABT.MaximumTension");

            if (enabled == null && idle == null && resting == null && maximum == null)
                return string.Empty;

            return (ToBool(enabled) ? "1" : "0")
                + "~" + Number(idle)
                + "~" + Number(resting)
                + "~" + Number(maximum);
        }

        private static object Read(PluginManager pluginManager, string name)
        {
            try { return pluginManager.GetPropertyValue(name); }
            catch { return null; }
        }

        private static string Number(object value)
        {
            double number;
            if (value == null || !double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return "--";
            return number.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static bool ToBool(object value)
        {
            if (value == null) return false;
            if (value is bool) return (bool)value;
            bool result;
            return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out result) && result;
        }
    }
}
