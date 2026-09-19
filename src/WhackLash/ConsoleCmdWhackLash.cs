using System.Collections.Generic;

namespace WhackLash
{
	/// <summary>
	/// <c>wl</c> (or <c>whacklash</c>). The bare command prints the settings block and changes
	/// nothing; every line of the block names the command that changes it, so it doubles as the
	/// menu. <c>wl info</c> adds the diagnostics and counters that answer "is this thing working".
	/// </summary>
	public class ConsoleCmdWhackLash : ConsoleCmdAbstract
	{
		public override bool IsExecuteOnClient => false;

		public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
		{
			string command = _params.Count > 0 ? _params[0].ToLower() : string.Empty;

			switch (command)
			{
			case "":
				OutputMenu("WhackLash is " + OnOff(Settings.Enabled));
				return;

			case "on":
			case "off":
				SetEnabled(command == "on");
				return;

			case "break":
				SetBreak(_params);
				return;

			case "zombies":
				SetTargets(_params, "zombies", ref Settings.Zombies);
				return;

			case "animals":
				SetTargets(_params, "animals", ref Settings.Animals);
				return;

			case "weights":
				SetWeights(_params);
				return;

			case "cap":
				SetCap(_params);
				return;

			case "decay":
				SetDecay(_params);
				return;

			case "bonus":
				SetBonus(_params);
				return;

			case "door":
				SetDoor(_params);
				return;

			case "flavor":
				SetFlavor(_params);
				return;

			case "info":
				OutputInfo();
				return;

			case "reset":
				Counters.Reset();
				Output("Counters reset.");
				return;

			default:
				Output("Unknown option '" + _params[0]
					+ "'. Try: wl [on|off|break|zombies|animals|weights|cap|decay|bonus|door|flavor {mod}|info|reset]");
				return;
			}
		}

		private static void OutputMenu(string _header)
		{
			Output(_header);
			Switch("wl on|off", OnOffChoices(Settings.Enabled), "build a focus meter on enemies you keep hitting");
			Line("wl break {points}|off", BreakLine());
			Switch("wl zombies on|off", OnOffChoices(Settings.Zombies), "zombies, zombie dogs and vultures build the meter");
			Switch("wl animals on|off", OnOffChoices(Settings.Animals), "hostile animals build the meter too");
			Line("wl weights {m} {a} {g}", WeightsLine());
			Line("wl cap {points}", CapLine());
			Line("wl decay {per sec}", DecayLine());
			Line("wl bonus {s} {d} {h} {r}", BonusLine());
			Line("wl door {pct} {min}", FlavorInterop.DoorStatus());
			FlavorLines();
		}

		/// <summary>The header says whether anything moved: typing the state you were already in
		/// should not read like a change.</summary>
		private static void SetEnabled(bool _on)
		{
			bool changed = Settings.Enabled != _on;
			Settings.Enabled = _on;
			if (changed)
			{
				Config.Save();
			}
			OutputMenu("WhackLash is " + (changed ? "now " : "already ") + OnOff(_on));
		}

		/// <summary>
		/// A number is the break point and switches the clamp on; 'off' leaves the vanilla pain
		/// meter alone throughout. 0 clamps from the first hit.
		/// </summary>
		private static void SetBreak(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: wl break {points}|off - currently: " + BreakLine()
					+ ". Meter points a zombie needs before it stops attacking through your hits; below "
					+ "that the vanilla pain meter runs as normal. 0 breaks it from the first hit.");
				return;
			}

			if (_params[1].ToLowerInvariant() == "off")
			{
				bool changed = Settings.NeutralizePainMeter;
				Settings.NeutralizePainMeter = false;
				if (changed)
				{
					Config.Save();
				}
				Output("Break " + (changed ? "now " : "already ") + "OFF - " + BreakLine() + ".");
				return;
			}

			if (!TryMeasure(_params[1], "break point", out float points))
			{
				return;
			}

			Settings.NeutralizePainMeter = true;
			Settings.BreakPoints = points;
			Config.Save();
			Output("Break: " + BreakLine());
		}

		private static void SetTargets(List<string> _params, string _what, ref bool _target)
		{
			if (!ReadOnOff(_params, "wl " + _what + " on|off", _target, out bool wanted))
			{
				return;
			}
			bool changed = _target != wanted;
			_target = wanted;
			if (changed)
			{
				Config.Save();
			}
			Output(char.ToUpperInvariant(_what[0]) + _what.Substring(1) + " " + (changed ? "now " : "already ")
				+ OnOff(wanted) + ".");
		}

		/// <summary>'on' or 'off' as the second word, else usage and the current state.</summary>
		private static bool ReadOnOff(List<string> _params, string _usage, bool _current, out bool _wanted)
		{
			_wanted = _current;
			string arg = _params.Count == 2 ? _params[1].ToLowerInvariant() : null;
			if (arg == "on" || arg == "off")
			{
				_wanted = arg == "on";
				return true;
			}
			Output("Usage: " + _usage + " - currently " + OnOff(_current));
			return false;
		}

		private static string OnOff(bool _on)
		{
			return _on ? "ON" : "OFF";
		}

		/// <summary>The menu, with the read-only lines appended in the same column.</summary>
		private static void OutputInfo()
		{
			OutputMenu("WhackLash is " + OnOff(Settings.Enabled));
			Line("settings file", Config.Status);
			Line("Undead Legacy", UndeadLegacyInfo.Status);
			for (int i = 0; i < FlavorPartners.All.Length; i++)
			{
				Line(FlavorPartners.All[i].Label, FlavorPartners.All[i].Status);
			}
			Line("hit hook (zombies)", Patches.ResponseHumanStatus);
			Line("hit hook (others)", Patches.ResponseAliveStatus);
			Line("damage patch", Patches.DamageStatus);
			Line("dismember patch", Patches.DismemberStatus);
			Line("stand-up patch", Patches.StandUpStatus);
			Line("hits", Counters.HitsCounted + " counted, " + Counters.HitsIgnored + " ignored");
			Line("bonus damage dealt", Counters.BonusDamage.ToString());
			Line("knockdown added", Counters.StunAdded + " points of build-up");
			Line("dismember boosted", Counters.DismemberBoosted + " rolls");
			Line("ragdolls forced", Counters.RagdollsForced.ToString());
			Line("stand-ups lifted", Counters.StandUpsLifted + " out of the ground");
			Line("pain meter clamps", Counters.PainClamped.ToString());
			Line("doors", Counters.DoorProcs + " slams handed over, " + Counters.DoorKnockdowns + " floored");
			Line("peak meter seen", Config.Number(FocusMeter.Peak) + " of " + Config.Number(Settings.Cap));
			Line("live meters", FocusMeter.LiveCount.ToString());

			if (Counters.HitsCounted == 0)
			{
				Output("Note: no hit has reached the hook yet. Hitting any zombie with a weapon should");
				Output("move that number - if it stays at zero, the hooks are not live.");
			}
		}

		/// <summary>Labels padded to the longest one ("wl bonus {s} {d} {h} {r}") so the block shares a column.</summary>
		private static void Line(string _label, string _value)
		{
			Output("  " + _label.PadRight(24) + ": " + _value);
		}

		/// <summary>A switch line: the choices padded to the widest set, then what the switch is for.</summary>
		private static void Switch(string _label, string _choices, string _note)
		{
			Line(_label, _choices.PadRight(15) + " - " + _note);
		}

		private static void SetWeights(List<string> _params)
		{
			if (_params.Count != 4)
			{
				Output("Usage: wl weights {melee} {archery} {gun} - currently: " + WeightsLine());
				return;
			}

			if (!TryMeasure(_params[1], "melee weight", out float melee)
				|| !TryMeasure(_params[2], "archery weight", out float archery)
				|| !TryMeasure(_params[3], "gun weight", out float gun))
			{
				return;
			}

			Settings.WeightMelee = melee;
			Settings.WeightArchery = archery;
			Settings.WeightGun = gun;
			Config.Save();
			Output("Weights: " + WeightsLine());
		}

		private static void SetCap(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: wl cap {points} - currently: " + CapLine());
				return;
			}

			if (!TryMeasure(_params[1], "cap", out float cap))
			{
				return;
			}

			Settings.Cap = cap;
			Config.Save();
			Output("Cap: " + CapLine());
		}

		private static void SetDecay(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: wl decay {points per second} - currently: " + DecayLine());
				return;
			}

			if (!TryMeasure(_params[1], "decay", out float decay))
			{
				return;
			}

			Settings.DecayPerSecond = decay;
			Config.Save();
			Output("Decay: " + DecayLine());
		}

		private static void SetBonus(List<string> _params)
		{
			if (_params.Count != 5)
			{
				Output("Usage: wl bonus {stun} {dismember} {damage} {ragdoll} - percent per point, currently: "
					+ BonusLine());
				return;
			}

			if (!TryMeasure(_params[1], "knockdown bonus", out float stun)
				|| !TryMeasure(_params[2], "dismember bonus", out float dismember)
				|| !TryMeasure(_params[3], "damage bonus", out float damage)
				|| !TryMeasure(_params[4], "ragdoll chance", out float ragdoll))
			{
				return;
			}

			Settings.StunPercent = stun;
			Settings.DismemberPercent = dismember;
			Settings.DamagePercent = damage;
			Settings.RagdollPercent = ragdoll;
			Config.Save();
			Output("Bonus: " + BonusLine());
		}

		private static void SetDoor(List<string> _params)
		{
			if (_params.Count != 3)
			{
				Output("Usage: wl door {percent per point} {min points} - currently: " + FlavorInterop.DoorStatus()
					+ ". The chance a door slammed on a zombie knocks it down, per meter point, and the "
					+ "points it needs first; needs DoorSlammer and 'wl flavor ds' on. 0 percent switches it off.");
				return;
			}

			if (!TryMeasure(_params[1], "door chance", out float percent)
				|| !TryMeasure(_params[2], "minimum points", out float min))
			{
				return;
			}

			Settings.DoorPercent = percent;
			Settings.DoorMinPoints = min;
			Config.Save();
			Output("Door: " + FlavorInterop.DoorStatus());
		}

		/// <summary>
		/// 'wl flavor' alone is a read. 'wl flavor {mod}' toggles that pair and mirrors it to that
		/// mod only; 'wl flavor on|off' sets and mirrors every pair.
		/// </summary>
		private static void SetFlavor(List<string> _params)
		{
			if (_params.Count < 2)
			{
				FlavorLines();
				return;
			}

			string arg = _params[1].ToLowerInvariant();
			if (arg == "on" || arg == "off")
			{
				bool on = arg == "on";
				FlavorSwitches.SetAll(on);
				Config.Save();
				FlavorPartners.PushAll(on);
				Output("Flavor " + OnOff(on) + " for every partner: "
					+ string.Join(", ", FlavorSwitches.Labels.ToArray()) + ".");
				return;
			}

			string label = FlavorSwitches.Resolve(_params[1]);
			if (label == null)
			{
				Output("'" + _params[1] + "' is not a partner this mod knows. Try: wl flavor ["
					+ string.Join("|", Aliases()) + "|on|off]");
				return;
			}

			bool now = !FlavorSwitches.IsOn(label);
			FlavorSwitches.Set(label, now);
			Config.Save();
			FlavorPartners.Push(label, now);
			Output(FlavorPartners.Describe(label));
		}

		/// <summary>One menu line per partner, known ones first.</summary>
		private static void FlavorLines()
		{
			foreach (string label in FlavorSwitches.Labels)
			{
				bool on = FlavorSwitches.IsOn(label);
				Switch("wl flavor " + FlavorPartners.AliasOf(label), OnOffChoices(on), FlavorPartners.MenuNote(label));
			}
		}

		private static string[] Aliases()
		{
			List<string> labels = FlavorSwitches.Labels;
			string[] aliases = new string[labels.Count];
			for (int i = 0; i < labels.Count; i++)
			{
				aliases[i] = FlavorPartners.AliasOf(labels[i]);
			}
			return aliases;
		}

		private static bool TryMeasure(string _value, string _what, out float _parsed)
		{
			if (Config.TryMeasure(_value, out _parsed))
			{
				return true;
			}
			Output("'" + _value + "' is not a valid " + _what + " - numbers from 0 up, like 0.5.");
			return false;
		}

		/// <summary>The choice list for a switch, with the live value marked.</summary>
		private static string OnOffChoices(bool _on)
		{
			return "[ " + Mark("on", _on) + " | " + Mark("off", !_on) + " ]";
		}

		private static string Mark(string _option, bool _live)
		{
			return _live ? ">" + _option + "<" : _option;
		}

		private static string WeightsLine()
		{
			return Config.Number(Settings.WeightMelee) + " melee / " + Config.Number(Settings.WeightArchery)
				+ " archery+thrown / " + Config.Number(Settings.WeightGun) + " gun+launcher per hit";
		}

		private static string BreakLine()
		{
			if (!Settings.NeutralizePainMeter)
			{
				return "off - the vanilla pain meter is left alone";
			}
			if (Settings.BreakPoints <= 0f)
			{
				return "zombie never attacks through, from the first hit";
			}
			return "zombie stops attacking through at " + Config.Number(Settings.BreakPoints) + " points";
		}

		private static string CapLine()
		{
			return "meter tops out at " + Config.Number(Settings.Cap) + " points";
		}

		private static string DecayLine()
		{
			return "meter drains " + Config.Number(Settings.DecayPerSecond) + " points per second";
		}

		private static string BonusLine()
		{
			return "per point +" + Config.Number(Settings.StunPercent) + "% knockdown, +"
				+ Config.Number(Settings.DismemberPercent) + "% dismember, +"
				+ Config.Number(Settings.DamagePercent) + "% damage, "
				+ Config.Number(Settings.RagdollPercent) + "% ragdoll on knockdown";
		}

		private static void Output(string _line)
		{
			SdtdConsole.Instance.Output(_line);
		}

		public override string[] getCommands()
		{
			return new string[2] { "wl", "whacklash" };
		}

		public override string getDescription()
		{
			return "Reports WhackLash's settings; 'wl on' and 'wl off' switch it.";
		}

		public override string getHelp()
		{
			return "Usage: wl [on|off|break {points}|off|zombies on|off|animals on|off|weights {m} {a} {g}"
				+ "|cap {points}|decay {per sec}|bonus {s} {d} {h} {r}|door {pct} {min}|flavor {mod}"
				+ "|flavor on|off|info|reset]"
				+ "\r\n\r\nEvery enemy you hit gets a focus meter. Each hit you land adds to it - a "
				+ "melee swing a full point, an arrow, bolt or thrown weapon half, a bullet or "
				+ "explosive a quarter - and it drains steadily from the moment of the last hit. "
				+ "While it is up, your hits on that enemy are worth more: they do more damage, add "
				+ "more towards knocking it down, dismember more often, and a knockdown can become a "
				+ "full ragdoll. Every bonus is a percentage per point, read off the meter as it "
				+ "stood before the hit, so the first hit of a chain earns nothing and every hit "
				+ "after it earns off the ones before. Five hits in a row are worth more than five "
				+ "hits over a minute."
				+ "\r\n\r\nThe game has a meter of its own that runs the other way: vanilla's pain "
				+ "meter fills as a zombie takes hits and, once full enough, lets it attack straight "
				+ "through yours and shrug off the slow that a flinch would cause. That stays in force "
				+ "until the focus meter reaches the break point, 3 by default: below it the zombie "
				+ "gets tougher exactly as in vanilla and the hits you land to build the meter are "
				+ "landed at risk; at it the zombie breaks, its pain meter is held down, every hit "
				+ "so every hit keeps it slowed and it cannot attack through - for as long as "
				+ "you keep the meter up there. 'wl break {points}' sets the break point (0 breaks it "
				+ "from the first hit); 'wl break off' leaves the vanilla pain meter alone and keeps "
				+ "only the bonuses here."
				+ "\r\n\r\n'wl' on its own prints the settings and changes nothing - it is the status "
				+ "read, so it is safe to type when you only want to look. Each line names the "
				+ "command that changes it, so the settings block is also the menu."
				+ "\r\n\r\n'wl on' and 'wl off' are the master switch. With it off every hook returns "
				+ "immediately: no meter, no bonuses, and the vanilla pain meter runs as normal. "
				+ "Saying which state you want rather than toggling it means the command reads the "
				+ "same whichever state you were in, and repeating it is harmless. 'wl zombies' and "
				+ "'wl animals' work the same way."
				+ "\r\n\r\n'wl zombies on|off' covers everything the game flags as a zombie - zombies, "
				+ "zombie dogs, vultures, Undead Legacy's own - and bandits. On by default. 'wl animals "
				+ "on|off' adds hostile animals: bears, wolves, boars, mountain lions. Off by default, "
				+ "since a bear you can break so it stops attacking through is a different animal."
				+ "\r\n\r\n'wl weights {melee} {archery} {gun}' sets the points one hit adds by what "
				+ "landed it: 1, 0.5 and 0.25 by default. Archery covers bows, crossbows and anything "
				+ "thrown; gun covers guns, launchers and explosives. A thrown spear carries the same "
				+ "item as a held one and counts as melee. Turrets, drones, traps, vehicles, and burns "
				+ "or bleeds count for nothing and earn nothing, whoever set them up."
				+ "\r\n\r\n'wl cap {points}' is the most the meter holds, 5 by default, and 'wl decay "
				+ "{per sec}' is how fast it drains, 0.2 a second by default, the same rate as the vanilla pain meter - "
				+ "so a full meter is gone 25 seconds after the last hit."
				+ "\r\n\r\n'wl bonus {stun} {dismember} {damage} {ragdoll}' sets the four payoffs, each "
				+ "a percentage per meter point: 20, 15, 5 and 15 by default. Stun is extra knockdown "
				+ "build-up per hit, as a share of the hit's damage, split between body and legs the way "
				+ "the game splits it. Dismember multiplies the weapon's dismember chance - and a head "
				+ "dismember is a kill, so this one is strong. Damage scales the hit itself, before "
				+ "armour. Ragdoll is the chance a knockdown the game would have played as an animated "
				+ "fall becomes a physics ragdoll instead. At a full meter of 5 that is double the "
				+ "knockdown build-up, 1.75x the dismember chance, 1.25x damage and a 75% ragdoll. 0 "
				+ "switches any one of them off."
				+ "\r\n\r\n'wl door {pct} {min}' is the DoorSlammer interaction: a door slammed on a "
				+ "zombie whose meter is at least {min} points knocks it down {pct} percent per point, "
				+ "20 and 1 by default, so a zombie you have hit four times goes down four slams in "
				+ "five. The ragdoll roll above applies to that knockdown too. The slam then counts as "
				+ "one melee hit. Needs DoorSlammer installed and the flavor switch on in both mods."
				+ "\r\n\r\n'wl flavor' lists the extra behaviour supported mods offer, one switch per "
				+ "mod, all on by default, and changes nothing. 'wl flavor {mod}' toggles one of them, "
				+ "by the other mod's command name - 'wl flavor ds' - and 'wl flavor on' or 'wl flavor "
				+ "off' sets them all. A switch does nothing unless that mod is installed. Each pair "
				+ "of mods is switched on both sides, and toggling it in either one sets both, so 'wl "
				+ "flavor ds' and 'ds flavor wl' are the same switch. A mod this build does not know "
				+ "about is let through until you switch it off; it gets a line of its own here once "
				+ "it has been seen."
				+ "\r\n\r\nIn multiplayer each machine works out its own hits and keeps its own copy of "
				+ "every meter, fed by the same damage traffic the server sees, so the settings on the "
				+ "machine that swung the weapon are the ones that apply to that swing. Keep them the "
				+ "same everywhere."
				+ "\r\n\r\nEvery setting here takes effect immediately and is written straight to a "
				+ "settings file, so it survives a restart - and survives updating the mod, because "
				+ "the file lives in the game's user data folder next to Saves rather than in Mods. "
				+ "'wl info' prints its full path. It is plain 'key = value' text and can be edited "
				+ "by hand with the game closed; a line that will not parse is ignored rather than "
				+ "fatal, and deleting the file goes back to the built-in defaults in Settings.cs."
				+ "\r\n\r\n'wl info' prints the same block with the patch state and the counters "
				+ "added. The key line is 'hits': the startup log only proves the hooks were "
				+ "installed, that number proves hits are reaching them. 'peak meter seen' shows how "
				+ "high a meter has climbed so far.\r\n\r\n'wl reset' zeroes the counters so one "
				+ "scenario can be measured on its own; live meters are left alone.\r\n\r\n'whacklash' "
				+ "is an alias for 'wl'.";
		}
	}
}
