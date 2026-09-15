namespace WhackLash
{
	/// <summary>
	/// The built-in defaults. <see cref="Config"/> overwrites these from the settings file at
	/// startup and the <c>wl</c> command changes them live; nothing else writes here.
	///
	/// Percentages are held as the number the player types ("20" for 20%), so a reported value
	/// can be typed straight back in. The maths divides by 100 where it is used.
	/// </summary>
	internal static class Settings
	{
		/// <summary>Master switch. When off, every patch returns immediately.</summary>
		internal static bool Enabled = true;

		/// <summary>
		/// Whether the vanilla pain meter is held below the point where it starts working for the
		/// zombie once its focus meter reaches <see cref="BreakPoints"/>. On (the default) means it
		/// is: a broken zombie stays slowed by every hit and cannot attack through. <c>wl break
		/// off</c> switches this off and leaves the vanilla meter alone throughout.
		/// </summary>
		internal static bool NeutralizePainMeter = true;

		/// <summary>
		/// Meter points, read before the hit, an enemy needs before the pain clamp engages. Below
		/// this vanilla rules run untouched, so the enemy earns attack-through as usual and you land
		/// hits at risk; at or above it the enemy is broken. 0 clamps from the first hit.
		/// </summary>
		internal static float BreakPoints = 3f;

		/// <summary>Zombies - and zombie dogs, vultures and anything else flagged zombie - take part.</summary>
		internal static bool Zombies = true;

		/// <summary>Hostile animals - bears, wolves, boars - take part too.</summary>
		internal static bool Animals;

		/// <summary>Meter points one hit adds, by what landed it.</summary>
		internal static float WeightMelee = 1f;

		/// <summary>Bows, crossbows and thrown weapons.</summary>
		internal static float WeightArchery = 0.5f;

		/// <summary>Guns, launchers and explosives.</summary>
		internal static float WeightGun = 0.25f;

		/// <summary>The meter never holds more than this many points.</summary>
		internal static float Cap = 5f;

		/// <summary>Points the meter loses per second, from the moment of the last hit.</summary>
		internal static float DecayPerSecond = 0.2f;

		/// <summary>Extra knockdown build-up per hit, percent of the hit's damage per meter point.</summary>
		internal static float StunPercent = 20f;

		/// <summary>Dismember chance bonus, percent per meter point.</summary>
		internal static float DismemberPercent = 15f;

		/// <summary>Damage bonus, percent per meter point.</summary>
		internal static float DamagePercent = 5f;

		/// <summary>Chance a knockdown becomes a ragdoll, percent per meter point.</summary>
		internal static float RagdollPercent = 15f;

		/// <summary>
		/// Chance a door slammed on a zombie knocks it down, percent per meter point. Only reachable
		/// through DoorSlammer with flavor on in both mods. 0 switches it off.
		/// </summary>
		internal static float DoorPercent = 20f;

		/// <summary>Meter points a zombie needs before a slammed door can floor it.</summary>
		internal static float DoorMinPoints = 1f;

		// The per-partner flavor switches live in FlavorSwitches: one per mod this one links up
		// with, on by default, written to the same settings file.
	}
}
