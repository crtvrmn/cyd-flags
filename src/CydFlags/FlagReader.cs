using System;
using System.Reflection;
using GameReaderCommon;

namespace CydFlags
{
    /// <summary>
    /// Reads SimHub's flag properties from StatusDataBase. Reflection is used
    /// so the plugin still loads on older SimHub builds that do not expose a
    /// given flag property yet.
    /// </summary>
    internal static class FlagReader
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        private static readonly PropertyInfo Green = Find("Flag_Green");
        private static readonly PropertyInfo Yellow = Find("Flag_Yellow");
        private static readonly PropertyInfo Blue = Find("Flag_Blue");
        private static readonly PropertyInfo White = Find("Flag_White");
        private static readonly PropertyInfo Checkered = Find("Flag_Checkered");
        private static readonly PropertyInfo Red = Find("Flag_Red");
        private static readonly PropertyInfo Black = Find("Flag_Black");
        private static readonly PropertyInfo Orange = Find("Flag_Orange");
        private static readonly PropertyInfo Name = Find("Flag_Name");

        private static PropertyInfo Find(string name)
        {
            try
            {
                return typeof(StatusDataBase).GetProperty(name, Flags);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static FlagKind ReadKind(StatusDataBase status)
        {
            if (status == null) return FlagKind.None;

            var bits = new FlagBits
            {
                Green = ReadBool(Green, status),
                Yellow = ReadBool(Yellow, status),
                Blue = ReadBool(Blue, status),
                White = ReadBool(White, status),
                Checkered = ReadBool(Checkered, status),
                Red = ReadBool(Red, status),
                Black = ReadBool(Black, status),
                Orange = ReadBool(Orange, status)
            };

            FlagKind kind = FlagModel.Select(bits);
            if (kind != FlagKind.None) return kind;

            // Some game plugins only fill the textual Flag_Name.
            return FlagModel.ParseName(ReadString(Name, status));
        }

        private static bool ReadBool(PropertyInfo property, StatusDataBase status)
        {
            if (property == null) return false;
            try
            {
                object value = property.GetValue(status, null);
                return value is bool && (bool)value;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ReadString(PropertyInfo property, StatusDataBase status)
        {
            if (property == null) return null;
            try
            {
                return property.GetValue(status, null) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
