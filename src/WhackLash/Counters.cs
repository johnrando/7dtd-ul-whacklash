namespace WhackLash
{
	/// <summary>
	/// Live counters behind <c>wl info</c>: the startup log proves the patches were installed,
	/// these prove hits are reaching them and what each one did. No locking: all writes happen on
	/// the main thread.
	/// </summary>
	internal static class Counters
	{
		/// <summary>Player hits that built the meter, door slams included.</summary>
		internal static int HitsCounted;

		/// <summary>Player hits from something that carries no weight: turrets, traps, vehicles, fire.</summary>
		internal static int HitsIgnored;

		/// <summary>Damage added on top of what the hits would have done.</summary>
		internal static int BonusDamage;

		/// <summary>Knockdown build-up added on top of the hits' own.</summary>
		internal static int StunAdded;

		/// <summary>Hits whose dismember roll was raised.</summary>
		internal static int DismemberBoosted;

		/// <summary>Knockdowns turned into ragdolls.</summary>
		internal static int RagdollsForced;

		/// <summary>Enemies that would have stood up inside the ground and were put on top of it.</summary>
		internal static int StandUpsLifted;

		/// <summary>Times the vanilla pain meter was held down.</summary>
		internal static int PainClamped;

		/// <summary>Hits where the banked pain resistance was put back before the flinch was chosen.</summary>
		internal static int PainRestored;

		/// <summary>Straight-on hits on a broken enemy played as hits from the side.</summary>
		internal static int SideFlinches;

		/// <summary>Flinches on a broken enemy turned into a sideways drop to a knee.</summary>
		internal static int SideFalls;

		/// <summary>Door slams DoorSlammer handed over.</summary>
		internal static int DoorProcs;

		/// <summary>Of those, the ones that floored the zombie.</summary>
		internal static int DoorKnockdowns;

		internal static void Reset()
		{
			HitsCounted = 0;
			HitsIgnored = 0;
			BonusDamage = 0;
			StunAdded = 0;
			DismemberBoosted = 0;
			RagdollsForced = 0;
			StandUpsLifted = 0;
			PainClamped = 0;
			PainRestored = 0;
			SideFlinches = 0;
			SideFalls = 0;
			DoorProcs = 0;
			DoorKnockdowns = 0;
			FocusMeter.Peak = 0f;
			FlavorPartners.ResetCounters();
		}
	}
}
