using System.Collections.Generic;
using UnityEngine;

namespace WhackLash
{
	/// <summary>
	/// One meter per enemy, keyed by entity id and shared by every player hitting it. A hit adds
	/// its weight, the meter drains at a fixed rate from the moment of the last hit, and it never
	/// holds more than the cap.
	///
	/// Decay is lazy: a meter stores its value and the time it was written, and every read
	/// subtracts what has drained since. No per-tick work, nothing to keep in step, and an enemy
	/// nobody is hitting costs nothing. Entries are pruned in passing once they have drained, and
	/// dropped outright when their enemy dies.
	///
	/// Every machine keeps its own copy. Hits reach clients through the same damage packets the
	/// server processes, so the copies agree to within network latency, which is all the payoffs
	/// need. Main thread only; no locking.
	///
	/// Each entry also banks the enemy's vanilla pain resistance while the enemy is broken: the
	/// value the game's own meter would hold if the clamp were not pinning it at 0.99 between
	/// hits. It drains at the game's fixed rate, 0.01 a tick, and is put back on the enemy just
	/// before each hit is played so the flinch is the length vanilla would have chosen. See
	/// <see cref="ResponsePatches"/> for why.
	/// </summary>
	internal static class FocusMeter
	{
		/// <summary>The vanilla pain meter's drain, 0.01 a tick at 20 ticks a second (<c>EntityAlive.OnUpdateLive</c>).</summary>
		private const float PainDecayPerSecond = 0.2f;

		private struct State
		{
			internal float Value;

			/// <summary>Banked vanilla pain resistance, 0 when nothing is banked.</summary>
			internal float Pain;

			/// <summary><see cref="Time.time"/> when Value and Pain were written.</summary>
			internal float Stamp;
		}

		private static readonly Dictionary<int, State> meters = new Dictionary<int, State>();

		/// <summary>Reused by <see cref="Prune"/> so a sweep allocates nothing.</summary>
		private static readonly List<int> drained = new List<int>();

		private static int writesSincePrune;

		/// <summary>The highest value any meter reached, for <c>wl info</c>. Zeroed by <c>wl reset</c>.</summary>
		internal static float Peak;

		/// <summary>Meters still on the books, drained or not; a sweep runs every so many writes.</summary>
		internal static int LiveCount
		{
			get { return meters.Count; }
		}

		/// <summary>The meter as it stands now, zero if the enemy has none or it has drained.</summary>
		internal static float Get(int _entityId)
		{
			if (!meters.TryGetValue(_entityId, out State state))
			{
				return 0f;
			}
			float value = state.Value - (Time.time - state.Stamp) * Settings.DecayPerSecond;
			return value > 0f ? value : 0f;
		}

		/// <summary>The banked pain resistance as it stands now, zero if none or it has drained.</summary>
		internal static float GetPain(int _entityId)
		{
			if (!meters.TryGetValue(_entityId, out State state) || state.Pain <= 0f)
			{
				return 0f;
			}
			float pain = state.Pain - (Time.time - state.Stamp) * PainDecayPerSecond;
			return pain > 0f ? pain : 0f;
		}

		/// <summary>
		/// Banks the enemy's pain resistance, or clears it with 0. The meter itself is carried
		/// over as it stands now, so this can be written before or after <see cref="Add"/>.
		/// </summary>
		internal static void SetPain(int _entityId, float _pain)
		{
			if (_pain <= 0f)
			{
				if (meters.TryGetValue(_entityId, out State state) && state.Pain > 0f)
				{
					meters[_entityId] = new State { Value = Get(_entityId), Pain = 0f, Stamp = Time.time };
				}
				return;
			}
			Write(_entityId, Get(_entityId), _pain);
		}

		/// <summary>Adds one hit's weight, capped. A nonsense result (a NaN from a bad setting) resets to zero.</summary>
		internal static void Add(int _entityId, float _weight)
		{
			float value = Get(_entityId) + _weight;
			if (value > Settings.Cap)
			{
				value = Settings.Cap;
			}
			if (float.IsNaN(value) || value < 0f)
			{
				value = 0f;
			}

			Write(_entityId, value, GetPain(_entityId));
			if (value > Peak)
			{
				Peak = value;
			}

			if (++writesSincePrune >= 64 || meters.Count > 256)
			{
				Prune();
			}
		}

		private static void Write(int _entityId, float _value, float _pain)
		{
			meters[_entityId] = new State { Value = _value, Pain = _pain, Stamp = Time.time };
		}

		/// <summary>The enemy is dead; whatever it had built no longer matters.</summary>
		internal static void Remove(int _entityId)
		{
			meters.Remove(_entityId);
		}

		private static void Prune()
		{
			writesSincePrune = 0;
			drained.Clear();
			foreach (KeyValuePair<int, State> entry in meters)
			{
				if (Get(entry.Key) <= 0f)
				{
					drained.Add(entry.Key);
				}
			}
			for (int i = 0; i < drained.Count; i++)
			{
				meters.Remove(drained[i]);
			}
			drained.Clear();
		}
	}
}
