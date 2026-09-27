using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WhackLash
{
	/// <summary>
	/// Reads <see cref="Settings"/> back at startup and writes it out whenever a <c>wl</c> command
	/// changes something. The file lives in the game's user data folder rather than in the mod
	/// folder, so it survives a mod update. Plain <c>key = value</c> text; every line names the
	/// console command that writes it. Nothing here can stop the mod working: any failure degrades
	/// to a log line and the defaults.
	/// </summary>
	internal static class Config
	{
		private const string FolderName = "WhackLash";

		private const string FileName = "settings.txt";

		/// <summary>What the last load or save did, as reported by <c>wl info</c>.</summary>
		internal static string Status = "not loaded - mod init has not run";

		/// <summary>Where the file is, once resolved. Null means it never was.</summary>
		private static string filePath;

		/// <summary>
		/// Called once from <see cref="ModApi.InitMod"/>, before the patches go in, so the startup
		/// log reports the player's settings rather than the defaults. A missing file is a first
		/// run: writing the defaults out is what makes the file discoverable.
		/// </summary>
		internal static void Load()
		{
			if (!Resolve())
			{
				return;
			}

			if (!File.Exists(filePath))
			{
				Save();
				return;
			}

			try
			{
				int applied = 0;
				int rejected = 0;
				foreach (string line in File.ReadAllLines(filePath))
				{
					switch (Parse(line))
					{
					case LineResult.Applied:
						applied++;
						break;
					case LineResult.Rejected:
						rejected++;
						break;
					}
				}

				Status = applied + " settings loaded"
					+ (rejected > 0 ? ", " + rejected + " line(s) ignored" : "")
					+ " - " + filePath;
				Log.Out(Patches.LogPrefix + "Settings loaded from " + filePath + ".");
			}
			catch (Exception e)
			{
				Status = "NOT LOADED - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not read " + filePath + ", so the built-in "
					+ "defaults are in force: " + e.Message);
			}
		}

		/// <summary>
		/// Writes the whole file, which is what keeps the comments and ordering intact. Called by
		/// every <c>wl</c> command that changes a setting and by <see cref="FlavorInterop.SetFlavor"/>.
		/// </summary>
		internal static void Save()
		{
			if (!Resolve())
			{
				return;
			}

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllText(filePath, Compose());
				Status = "saved - " + filePath;
			}
			catch (Exception e)
			{
				Status = "NOT SAVED - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not write " + filePath + ", so this change "
					+ "will not survive a restart: " + e.Message);
			}
		}

		/// <summary>Works out where the file goes, once.</summary>
		private static bool Resolve()
		{
			if (filePath != null)
			{
				return true;
			}

			try
			{
				string dir = GameIO.GetUserGameDataDir();
				if (string.IsNullOrEmpty(dir))
				{
					Status = "unavailable - the game reported no user data folder";
					return false;
				}
				filePath = Path.Combine(Path.Combine(dir, FolderName), FileName);
				return true;
			}
			catch (Exception e)
			{
				Status = "unavailable - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not work out where to keep settings, so they "
					+ "will not persist: " + e.Message);
				return false;
			}
		}

		private static string Compose()
		{
			StringBuilder text = new StringBuilder();
			text.AppendLine("# WhackLash settings.");
			text.AppendLine("#");
			text.AppendLine("# Read once when the game starts and rewritten whenever a 'wl' command changes");
			text.AppendLine("# something, so edit this with the game closed. Every line names the command that");
			text.AppendLine("# sets it; anything after a '#' is a comment, and a line that will not parse is");
			text.AppendLine("# ignored rather than fatal. Percentages are per meter point.");
			text.AppendLine();
			Setting(text, "enabled", OnOff(Settings.Enabled), "wl on|off");
			Setting(text, "break", Settings.NeutralizePainMeter ? Number(Settings.BreakPoints) : "off",
				"wl break {points}|off - meter points before the zombie stops attacking through; off leaves the vanilla pain meter alone");
			Setting(text, "targets.zombies", OnOff(Settings.Zombies), "wl zombies on|off");
			Setting(text, "targets.animals", OnOff(Settings.Animals), "wl animals on|off");
			Setting(text, "weight.melee", Number(Settings.WeightMelee), "wl weights {melee} {archery} {gun}");
			Setting(text, "weight.archery", Number(Settings.WeightArchery), "wl weights {melee} {archery} {gun} - bows, crossbows, thrown");
			Setting(text, "weight.gun", Number(Settings.WeightGun), "wl weights {melee} {archery} {gun} - guns, launchers, explosives");
			Setting(text, "cap", Number(Settings.Cap), "wl cap {points}");
			Setting(text, "decay", Number(Settings.DecayPerSecond), "wl decay {points per second}");
			Setting(text, "bonus.stun", Number(Settings.StunPercent), "wl bonus {stun} {dismember} {damage} {ragdoll}");
			Setting(text, "bonus.dismember", Number(Settings.DismemberPercent), "wl bonus {stun} {dismember} {damage} {ragdoll}");
			Setting(text, "bonus.damage", Number(Settings.DamagePercent), "wl bonus {stun} {dismember} {damage} {ragdoll}");
			Setting(text, "bonus.ragdoll", Number(Settings.RagdollPercent), "wl bonus {stun} {dismember} {damage} {ragdoll}");
			Setting(text, "side.flinch", Number(Settings.SidePercent), "wl side {flinch} {fall} - once broken, a straight-on hit played from the side");
			Setting(text, "side.fall", Number(Settings.SideFallPercent), "wl side {flinch} {fall} - once broken, a flinch becomes a sideways drop to a knee");
			Setting(text, "door.percent", Number(Settings.DoorPercent), "wl door {pct} {min}");
			Setting(text, "door.min", Number(Settings.DoorMinPoints), "wl door {pct} {min}");
			foreach (string label in FlavorSwitches.Labels)
			{
				Setting(text, "flavor." + label.ToLowerInvariant(), OnOff(FlavorSwitches.IsOn(label)),
					"wl flavor " + FlavorPartners.AliasOf(label));
			}
			return text.ToString();
		}

		/// <summary>One setting, padded so the values and the commands each share a column.</summary>
		private static void Setting(StringBuilder _text, string _key, string _value, string _command)
		{
			_text.AppendLine(_key.PadRight(20) + "= " + _value.PadRight(8) + " # " + _command);
		}

		private enum LineResult
		{
			/// <summary>Blank or a comment.</summary>
			Skipped,

			Applied,

			Rejected
		}

		/// <summary>
		/// One line of the file. An unknown key is a warning rather than an error: that is what a
		/// file written by a newer version of the mod looks like to an older one.
		/// </summary>
		private static LineResult Parse(string _line)
		{
			int comment = _line.IndexOf('#');
			string text = (comment >= 0 ? _line.Substring(0, comment) : _line).Trim();
			if (text.Length == 0)
			{
				return LineResult.Skipped;
			}

			int split = text.IndexOf('=');
			if (split <= 0)
			{
				Log.Warning(Patches.LogPrefix + "Ignoring a settings line that is not 'key = value': "
					+ _line.Trim());
				return LineResult.Rejected;
			}

			string key = text.Substring(0, split).Trim().ToLowerInvariant();
			string value = text.Substring(split + 1).Trim();
			if (Apply(key, value))
			{
				return LineResult.Applied;
			}

			Log.Warning(Patches.LogPrefix + "Ignoring settings line '" + _line.Trim()
				+ "' - unknown setting or unusable value.");
			return LineResult.Rejected;
		}

		private static bool Apply(string _key, string _value)
		{
			switch (_key)
			{
			case "enabled":
				return TryBool(_value, ref Settings.Enabled);
			case "break":
				return TryBreak(_value);
			case "targets.zombies":
				return TryBool(_value, ref Settings.Zombies);
			case "targets.animals":
				return TryBool(_value, ref Settings.Animals);
			case "weight.melee":
				return LoadMeasure(_value, ref Settings.WeightMelee);
			case "weight.archery":
				return LoadMeasure(_value, ref Settings.WeightArchery);
			case "weight.gun":
				return LoadMeasure(_value, ref Settings.WeightGun);
			case "cap":
				return LoadMeasure(_value, ref Settings.Cap);
			case "decay":
				return LoadMeasure(_value, ref Settings.DecayPerSecond);
			case "bonus.stun":
				return LoadMeasure(_value, ref Settings.StunPercent);
			case "bonus.dismember":
				return LoadMeasure(_value, ref Settings.DismemberPercent);
			case "bonus.damage":
				return LoadMeasure(_value, ref Settings.DamagePercent);
			case "bonus.ragdoll":
				return LoadMeasure(_value, ref Settings.RagdollPercent);
			case "side.flinch":
				return LoadMeasure(_value, ref Settings.SidePercent);
			case "side.fall":
				return LoadMeasure(_value, ref Settings.SideFallPercent);
			case "door.percent":
				return LoadMeasure(_value, ref Settings.DoorPercent);
			case "door.min":
				return LoadMeasure(_value, ref Settings.DoorMinPoints);
			case "flavor":
				// The single switch older builds might write: apply it to every partner.
				return TryFlavor(null, _value);
			default:
				// flavor.<mod>: one partner's switch. Any label is accepted, so a switch a mod
				// this build does not know about created is kept.
				return _key.StartsWith("flavor.") && _key.Length > 7
					&& TryFlavor(_key.Substring(7), _value);
			}
		}

		/// <summary>'off' leaves the vanilla pain meter alone; a number is the break point and switches the clamp on.</summary>
		private static bool TryBreak(string _value)
		{
			if (_value.Trim().ToLowerInvariant() == "off")
			{
				Settings.NeutralizePainMeter = false;
				return true;
			}
			if (!LoadMeasure(_value, ref Settings.BreakPoints))
			{
				return false;
			}
			Settings.NeutralizePainMeter = true;
			return true;
		}

		/// <summary>One partner's switch, or every partner's when the label is null.</summary>
		private static bool TryFlavor(string _label, string _value)
		{
			bool on = false;
			if (!TryBool(_value, ref on))
			{
				return false;
			}
			if (_label == null)
			{
				FlavorSwitches.SetAll(on);
			}
			else
			{
				FlavorSwitches.Set(_label, on);
			}
			return true;
		}

		/// <summary>Accepts what the file writes plus the obvious hand-edit synonyms.</summary>
		private static bool TryBool(string _value, ref bool _target)
		{
			switch (_value.ToLowerInvariant())
			{
			case "on":
			case "true":
			case "yes":
			case "1":
				_target = true;
				return true;
			case "off":
			case "false":
			case "no":
			case "0":
				_target = false;
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// Points, seconds or percentages, zero or more, against the invariant culture so a file
		/// written on one machine means the same on one whose decimal separator is a comma. Shared
		/// with the console command. <c>!(x &gt;= 0)</c> rather than <c>x &lt; 0</c> so NaN is rejected too.
		/// </summary>
		internal static bool TryMeasure(string _value, out float _parsed)
		{
			return float.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out _parsed)
				&& _parsed >= 0f && !float.IsInfinity(_parsed);
		}

		private static bool LoadMeasure(string _value, ref float _target)
		{
			if (!TryMeasure(_value, out float parsed))
			{
				return false;
			}
			_target = parsed;
			return true;
		}

		private static string OnOff(bool _on)
		{
			return _on ? "on" : "off";
		}

		/// <summary>Written the way it is parsed, so a reported value can be typed back in.</summary>
		internal static string Number(float _value)
		{
			return _value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
