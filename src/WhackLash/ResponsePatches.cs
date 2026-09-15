namespace WhackLash
{
	/// <summary>
	/// The body of the patch on <c>ProcessDamageResponseLocal</c>, the method where a hit that has
	/// already been decided is played on the enemy: the flinch, the knockdown, the pain meter, the
	/// health loss. Everything here runs after that body, and two things happen.
	///
	/// The vanilla pain meter is held below 1 - but only once the enemy's focus meter, read as it
	/// stood before this hit, has reached the break point. Below that the vanilla meter runs
	/// untouched: the enemy earns its attack-through on the second hit as the game intends, and the
	/// hits that build the meter are landed at risk. Holding it down on every hit made a bare fist a
	/// stun-lock, which is what the break point is for.
	///
	/// The clamp is deliberately a postfix and nothing else. Everything it is for reads
	/// <c>painResistPercent</c> <em>between</em> hits - <c>EntityAlive.IsAttackValid</c> at
	/// <c>:5947</c> waves the attack through on <c>>= 1</c>, <c>EntityMoveHelper</c> at <c>:460</c>
	/// drops the enemy to a tenth speed while it is under 1 - so leaving the meter at 0.99 once the
	/// body has finished denies the attack-through and keeps the slow just as surely. What a prefix
	/// would also change is the flinch: the body picks the hit animation's duration from the
	/// <em>post-increment</em> value (<c>EntityAlive.cs:4941-4955</c>, UL's re-implementation at
	/// <c>H_Equipment.cs:383</c>), and under 1 that duration is <c>float.MaxValue</c> - the full
	/// hit reaction, uncut and re-triggered by every following hit, which on a running enemy is the
	/// long stagger. Holding the meter down before the body ran turned every hit past the break
	/// point into one of those. Letting the body see the true value leaves the game's own short
	/// flinch in place and costs nothing the clamp was for.
	///
	/// The hit is then scored: the enemy's meter is read as it stood before this hit, the knockdown
	/// and ragdoll bonuses are applied off that, and only then does the hit add its weight. So the
	/// first hit of a chain earns nothing, and every hit after it earns off the hits before.
	///
	/// Two sites share this body, because under Undead Legacy a zombie never enters the patched
	/// <c>EntityAlive</c> method at all - see <see cref="Patches"/>. Without Undead Legacy the
	/// <c>EntityHuman</c> override calls base, so both sites fire on one hit, and the
	/// <c>EntityAlive</c> site stands down for humans to avoid counting twice.
	/// </summary>
	internal static class ResponsePatches
	{
		private const float Ceiling = 0.99f;

		/// <summary>Runs at <c>Priority.Last</c>, after Undead Legacy's postfix has played the hit.</summary>
		internal static void Postfix(EntityAlive __instance, DamageResponse _dmResponse, bool _fromHumanSite)
		{
			if (!Settings.Enabled)
			{
				return;
			}
			if (!_fromHumanSite && __instance is EntityHuman)
			{
				return;
			}
			if (!HitClassifier.Qualifies(__instance))
			{
				if (__instance != null && __instance.IsDead())
				{
					FocusMeter.Remove(__instance.entityId);
				}
				return;
			}

			// Read before Add below, so the gate sees the meter as it stood before this hit. A class
			// the game marks as feeling no pain (-1 per hit) never raises the meter in the first
			// place, so there is nothing here to hold down and nothing to count.
			if (Settings.NeutralizePainMeter && __instance.painResistPercent > Ceiling && IsBroken(__instance))
			{
				__instance.painResistPercent = Ceiling;
				Counters.PainClamped++;
			}

			if (_dmResponse.Fatal)
			{
				FocusMeter.Remove(__instance.entityId);
				return;
			}

			DamageSource source = _dmResponse.Source;
			if (source == null || source.BuffClass != null)
			{
				return;
			}
			if (HitClassifier.Attacker(__instance, source) == null)
			{
				return;
			}

			float weight = HitClassifier.Weight(source);
			if (weight <= 0f)
			{
				Counters.HitsIgnored++;
				return;
			}

			float meter = FocusMeter.Get(__instance.entityId);
			if (meter > 0f)
			{
				AddKnockdown(__instance, _dmResponse, meter);
				TryRagdoll(__instance, _dmResponse, meter);
			}

			FocusMeter.Add(__instance.entityId, weight);
			Counters.HitsCounted++;
		}

		/// <summary>Whether the enemy's focus meter, as it stands before this hit, has reached the break point.</summary>
		private static bool IsBroken(EntityAlive _entity)
		{
			return FocusMeter.Get(_entity.entityId) >= Settings.BreakPoints;
		}

		/// <summary>
		/// The game keeps two knockdown accumulators, one for the body and one for the legs, fed by
		/// every stunning hit's damage and compared to a share of max health on the next hit. This
		/// adds a slice more, split by body part exactly as the game splits it. A cop's Special part
		/// is neither, and gets nothing, as in vanilla. Sleepers and the already-stunned get nothing
		/// either: the game computes no knockdown for them.
		/// </summary>
		private static void AddKnockdown(EntityAlive _entity, DamageResponse _dmResponse, float _meter)
		{
			if (Settings.StunPercent <= 0f || _entity.bodyDamage.CurrentStun != EnumEntityStunType.None
				|| !_dmResponse.Source.CanStun || _entity.sleepingOrWakingUp)
			{
				return;
			}

			int extra = HitClassifier.RoundHalfUp(_dmResponse.Strength * _meter * Settings.StunPercent / 100f);
			if (extra <= 0)
			{
				return;
			}

			EnumBodyPartHit part = _dmResponse.HitBodyPart;
			if ((part & (EnumBodyPartHit.Arms | EnumBodyPartHit.Torso | EnumBodyPartHit.Head)) > EnumBodyPartHit.None)
			{
				_entity.bodyDamage.StunProne += extra;
			}
			else if (part.IsLeg())
			{
				_entity.bodyDamage.StunKnee += extra;
			}
			else
			{
				return;
			}
			Counters.StunAdded += extra;
		}

		/// <summary>
		/// A knockdown the game played as the animated fall can become the physics ragdoll instead.
		/// The game itself ragdolls an enemy that is already in a stun animation, so switching
		/// mid-fall is nothing it does not do. Skipped when the game already ragdolled this hit.
		/// </summary>
		private static void TryRagdoll(EntityAlive _entity, DamageResponse _dmResponse, float _meter)
		{
			if (Settings.RagdollPercent <= 0f || _dmResponse.Stun != EnumEntityStunType.Prone
				|| _entity.emodel == null || _entity.emodel.IsRagdollActive)
			{
				return;
			}
			if (_entity.rand.RandomFloat >= _meter * Settings.RagdollPercent / 100f)
			{
				return;
			}

			_entity.DoRagdoll(_dmResponse);
			Counters.RagdollsForced++;
		}
	}
}
