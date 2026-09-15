using System;
using System.Reflection;
using HarmonyLib;

namespace WhackLash
{
	/// <summary>
	/// Installs the Harmony patches. Each is resolved late and gated on its own prerequisites, so a
	/// game update that moves something degrades to a log line rather than an exception at init.
	///
	/// Four sites. Two are on <c>ProcessDamageResponseLocal</c>, the method that plays a decided
	/// hit on the enemy, and there are two of them because of how Undead Legacy patches it:
	///
	/// - UL prefixes <c>EntityHuman.ProcessDamageResponseLocal</c> with its rage roll, runs a
	///   reverse-patched copy of the <em>unpatched</em> <c>EntityAlive</c> body, and returns false.
	///   So under UL a zombie never enters the patched <c>EntityAlive</c> method at all, and only a
	///   patch on the <c>EntityHuman</c> override sees it.
	/// - Everything that is not a human - zombie dogs, vultures, wolves, bears - does enter the
	///   <c>EntityAlive</c> method, where UL's equipment prefix skips the original and its postfix
	///   re-implements the body.
	///
	/// Both response sites are postfix-only, at <c>Priority.Last</c> so they see the state UL's
	/// postfix left. Postfixes always run, skipped original or not - which is the whole reason this
	/// works under UL, where the original never runs at all. Without UL the override calls base and
	/// both sites fire on one hit, so the <c>EntityAlive</c> postfix stands down for humans.
	///
	/// The other two are prefixes on the methods that decide a hit - <c>DamageEntity</c> for the
	/// damage and <c>CheckDismember</c> for the dismember roll - which UL does not touch for enemies.
	///
	/// No load order needs declaring: UL applies its patches as a BepInEx plugin before any
	/// IModApi.InitMod runs, and ModManager loads every mod assembly before calling any InitMod.
	/// </summary>
	internal static class Patches
	{
		internal const string LogPrefix = "[WhackLash] ";

		private const string HarmonyId = "WhackLash";

		private const string NotRunYet = "not applied - mod init has not run";

		/// <summary>Outcome of each patch, as reported by <c>wl info</c>.</summary>
		internal static string ResponseHumanStatus = NotRunYet;

		internal static string ResponseAliveStatus = NotRunYet;

		internal static string DamageStatus = NotRunYet;

		internal static string DismemberStatus = NotRunYet;

		private static bool applied;

		internal static void Apply()
		{
			if (applied)
			{
				return;
			}
			applied = true;
			try
			{
				ApplyPatches();
			}
			catch (Exception e)
			{
				Log.Error(LogPrefix + "Failed to apply patches; hits will build no meter.");
				Log.Exception(e);
			}
		}

		private static void ApplyPatches()
		{
			UndeadLegacyInfo.Report();
			FlavorPartners.Resolve();

			Harmony harmony = new Harmony(HarmonyId);
			// Spelled out per site so ul-decomp's patch_surface.py --collides-with can see both.
			ResponseHumanStatus = ApplyResponseSite(harmony,
				AccessTools.DeclaredMethod(typeof(EntityHuman), "ProcessDamageResponseLocal", new[] { typeof(DamageResponse) }),
				"EntityHuman.ProcessDamageResponseLocal", nameof(HumanPostfix), "zombies");
			ResponseAliveStatus = ApplyResponseSite(harmony,
				AccessTools.DeclaredMethod(typeof(EntityAlive), "ProcessDamageResponseLocal", new[] { typeof(DamageResponse) }),
				"EntityAlive.ProcessDamageResponseLocal", nameof(AlivePostfix), "other enemies");
			ApplyDamage(harmony);
			ApplyDismember(harmony);

			Log.Out(LogPrefix + "Vanilla pain meter is " + (Settings.NeutralizePainMeter
				? "held down from " + Config.Number(Settings.BreakPoints) + " meter points"
				: "left alone") + " (change with 'wl break {points}|off').");
		}

		/// <summary>One of the two response sites; see the class comment for why there are two.</summary>
		private static string ApplyResponseSite(Harmony _harmony, MethodInfo _target, string _name,
			string _postfixName, string _covers)
		{
			if (_target == null)
			{
				Log.Error(LogPrefix + "Hit hook for " + _covers + " NOT applied: " + _name
					+ " could not be found, so their hits will build no meter.");
				return "NOT APPLIED - " + _name + " not found";
			}

			_harmony.Patch(_target,
				postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Patches), _postfixName))
				{
					priority = Priority.Last
				});

			Log.Out(LogPrefix + "Hit hook applied for " + _covers + ": postfix on " + _name + ".");
			return "applied - postfix on " + _name;
		}

		/// <summary>The <c>EntityHuman</c> site: every zombie, under Undead Legacy or not.</summary>
		private static void HumanPostfix(EntityAlive __instance, DamageResponse _dmResponse)
		{
			ResponsePatches.Postfix(__instance, _dmResponse, _fromHumanSite: true);
		}

		/// <summary>The <c>EntityAlive</c> site: everything that is not a human.</summary>
		private static void AlivePostfix(EntityAlive __instance, DamageResponse _dmResponse)
		{
			ResponsePatches.Postfix(__instance, _dmResponse, _fromHumanSite: false);
		}

		private static void ApplyDamage(Harmony _harmony)
		{
			MethodInfo target = AccessTools.DeclaredMethod(typeof(EntityAlive), "DamageEntity",
				new[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) });
			if (target == null)
			{
				DamageStatus = "NOT APPLIED - EntityAlive.DamageEntity not found";
				Log.Warning(LogPrefix + "Damage bonus NOT applied: EntityAlive.DamageEntity could not be "
					+ "found. The meter still builds and the other bonuses still work.");
				return;
			}

			_harmony.Patch(target, prefix: new HarmonyMethod(
				AccessTools.DeclaredMethod(typeof(DamagePatches), nameof(DamagePatches.DamagePrefix))));

			DamageStatus = "applied - prefix on EntityAlive.DamageEntity";
			Log.Out(LogPrefix + "Damage bonus applied: prefix on EntityAlive.DamageEntity.");
		}

		private static void ApplyDismember(Harmony _harmony)
		{
			MethodInfo target = AccessTools.DeclaredMethod(typeof(EntityAlive), "CheckDismember",
				new[] { typeof(DamageResponse).MakeByRefType(), typeof(float) });
			if (target == null)
			{
				DismemberStatus = "NOT APPLIED - EntityAlive.CheckDismember not found";
				Log.Warning(LogPrefix + "Dismember bonus NOT applied: EntityAlive.CheckDismember could not "
					+ "be found. The meter still builds and the other bonuses still work.");
				return;
			}

			_harmony.Patch(target, prefix: new HarmonyMethod(
				AccessTools.DeclaredMethod(typeof(DamagePatches), nameof(DamagePatches.DismemberPrefix))));

			DismemberStatus = "applied - prefix on EntityAlive.CheckDismember";
			Log.Out(LogPrefix + "Dismember bonus applied: prefix on EntityAlive.CheckDismember.");
		}
	}
}
