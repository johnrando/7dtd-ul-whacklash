using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace WhackLash
{
	/// <summary>
	/// Detects whether Undead Legacy is loaded and which build it is. Purely advisory: this mod
	/// patches only vanilla types, so nothing here gates anything.
	///
	/// Of UL's version markers only the ones on <c>H_UndeadLegacy</c> are trustworthy: the
	/// <c>[BepInPlugin]</c> attribute and the <c>pluginVersion</c> literal. UL's assembly version is
	/// hardcoded 1.0.0.0 and its ModInfo.xml lags reality.
	/// </summary>
	internal static class UndeadLegacyInfo
	{
		private const string AssemblyName = "UndeadLegacy";

		/// <summary>Newest Undead Legacy build this mod was verified against. Advisory only; the
		/// <c>info</c> command prints it next to the detected build.</summary>
		internal const string TestedTo = "2.7.36";

		internal static bool Present;

		internal static string Version = "not detected";

		internal static string DetectedSource = "none";

		/// <summary>One-line summary for the <c>ds</c> console command.</summary>
		internal static string Status = "not checked";

		internal static void Report()
		{
			Assembly assembly = FindAssembly(AssemblyName);
			Present = assembly != null;
			if (!Present)
			{
				Status = "not installed";
				Log.Out(Patches.LogPrefix + "Undead Legacy is not installed; running against vanilla "
					+ "zombies. Everything works the same.");
				return;
			}

			Type plugin = assembly.GetType("H_UndeadLegacy", false);
			string raw = plugin == null
				? null
				: ReadFromBepInPluginAttribute(plugin) ?? ReadFromVersionConstant(plugin);

			if (raw == null)
			{
				Version = "unknown";
				Status = "installed, version unknown";
				Log.Out(Patches.LogPrefix + "Undead Legacy is installed but its version could not be "
					+ "read. This mod patches only vanilla types, so that is not a problem.");
				return;
			}

			Version = raw;
			Status = "Detected v" + raw + " / Tested up to v" + TestedTo;
			Log.Out(Patches.LogPrefix + "Undead Legacy " + raw + " detected (from " + DetectedSource
				+ "). This mod patches only vanilla types that UL inherits, so no UL code is touched.");
		}

		/// <summary>
		/// Reads the third argument of <c>[BepInPlugin(guid, name, version)]</c> via
		/// <see cref="CustomAttributeData"/>, so this assembly needs no reference to BepInEx.
		/// </summary>
		private static string ReadFromBepInPluginAttribute(Type _plugin)
		{
			try
			{
				foreach (CustomAttributeData attribute in CustomAttributeData.GetCustomAttributes(_plugin))
				{
					if (attribute.Constructor?.DeclaringType?.Name != "BepInPlugin")
					{
						continue;
					}
					IList<CustomAttributeTypedArgument> args = attribute.ConstructorArguments;
					if (args.Count >= 3 && args[2].Value is string version && version.Length > 0)
					{
						DetectedSource = "[BepInPlugin] attribute";
						return version;
					}
				}
			}
			catch (Exception e)
			{
				Log.Warning(Patches.LogPrefix + "Could not read Undead Legacy's [BepInPlugin] attribute: "
					+ e.Message);
			}
			return null;
		}

		private static string ReadFromVersionConstant(Type _plugin)
		{
			try
			{
				FieldInfo field = AccessTools.Field(_plugin, "pluginVersion");
				if (field != null && field.IsLiteral && field.GetRawConstantValue() is string version
					&& version.Length > 0)
				{
					DetectedSource = "pluginVersion constant";
					return version;
				}
			}
			catch (Exception e)
			{
				Log.Warning(Patches.LogPrefix + "Could not read Undead Legacy's pluginVersion constant: "
					+ e.Message);
			}
			return null;
		}

		/// <summary>A loaded assembly by simple name, or null. Shared with <see cref="FlavorPartner"/>.</summary>
		internal static Assembly FindAssembly(string _simpleName)
		{
			Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < loaded.Length; i++)
			{
				if (string.Equals(loaded[i].GetName().Name, _simpleName, StringComparison.OrdinalIgnoreCase))
				{
					return loaded[i];
				}
			}
			return null;
		}
	}
}
