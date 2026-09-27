namespace WhackLash
{
	/// <summary>
	/// The bodies of the patches on <c>ProcessDamageResponseLocal</c>, the method where a hit that
	/// has already been decided is played on the enemy: the flinch, the knockdown, the pain meter,
	/// the health loss. A prefix runs ahead of the body and a postfix after it, and between them
	/// three things happen.
	///
	/// <b>The vanilla pain meter is held below 1</b> - but only once the enemy's focus meter, read
	/// as it stood before this hit, has reached the break point. Below that the vanilla meter runs
	/// untouched: the enemy earns its attack-through on the second hit as the game intends, and
	/// the hits that build the meter are landed at risk. Holding it down on every hit made a bare
	/// fist a stun-lock, which is what the break point is for. Everything the clamp is for reads
	/// <c>painResistPercent</c> <em>between</em> hits - <c>EntityAlive.IsAttackValid</c> at
	/// <c>:5947</c> waves the attack through on <c>>= 1</c>, <c>EntityMoveHelper</c> at <c>:460</c>
	/// drops the enemy to a tenth speed while it is under 1 - so the clamp is applied after the
	/// body, in the postfix, and the enemy sits at 0.99 until the next hit.
	///
	/// <b>The pain the clamp discards is banked and put back for the hit.</b> The body picks the
	/// flinch's length from the <em>post-increment</em> value (<c>EntityAlive.cs:4941-4955</c>,
	/// UL's re-implementation at <c>H_Equipment.cs:383</c>): under 1 it is <c>float.MaxValue</c>,
	/// the full hit reaction, uncut and re-triggered by every following hit, which on a running
	/// enemy is the long forward stumble; from 1 up it shortens towards 0.15 s as the meter climbs
	/// to 3. Vanilla drains the meter 0.2 a second from wherever it stands, so an enemy that has
	/// banked 2.5 over a chain is still on a short flinch after a four-second gap. Clamped to
	/// 0.99 it is at zero after five, and the next hit - the first one after a knockdown, a
	/// ragdoll, a reload - plays the long stumble; and even inside a chain, 0.99 plus one hit is a
	/// 0.45 s flinch where vanilla's is 0.15. So the postfix banks the value the body produced
	/// before clamping it, the bank drains at vanilla's rate, and the prefix puts it back on the
	/// enemy just before the body runs. The body then chooses exactly the flinch vanilla would
	/// have; the clamp still lands after it. Once the meter lapses below the break point the
	/// restored value simply stays on the enemy, as vanilla's would have.
	///
	/// <b>A broken enemy's hits can go sideways.</b> Every hit carries a direction, front, back,
	/// left or right, worked out from where it came from, and the hit reaction, the animated
	/// knockdown and the kneel all take it. Once the enemy is broken the prefix rolls a
	/// straight-on hit over to a side, so the game plays its own side flinch or side fall; and it
	/// rolls a hit that would only flinch into a drop to a knee sideways, filled in on the
	/// response the same way the game fills in a leg knockdown, so the body plays it as one. Both
	/// rolls draw on the hit's own random number, which travels in the damage packet, so every
	/// machine makes the same call.
	///
	/// <b>The hit is then scored:</b> the enemy's meter is read as it stood before this hit, the
	/// knockdown and ragdoll bonuses are applied off that, and only then does the hit add its
	/// weight. So the first hit of a chain earns nothing, and every hit after it earns off the
	/// hits before.
	///
	/// Two sites share these bodies, because under Undead Legacy a zombie never enters the patched
	/// <c>EntityAlive</c> method at all - see <see cref="Patches"/>. Without Undead Legacy the
	/// <c>EntityHuman</c> override calls base, so both sites fire on one hit, and the
	/// <c>EntityAlive</c> site stands down for humans to avoid counting twice.
	/// </summary>
	internal static class ResponsePatches
	{
		private const float Ceiling = 0.99f;

		/// <summary>
		/// Runs at <c>Priority.First</c>, ahead of Undead Legacy's prefixes, which skip everything
		/// below them. Never skips the original itself. The response is a struct; a change made
		/// here is what the body, UL's copy of it and the postfixes all see.
		/// </summary>
		internal static void Prefix(EntityAlive __instance, ref DamageResponse _dmResponse, bool _fromHumanSite)
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
				return;
			}

			if (Settings.NeutralizePainMeter)
			{
				float banked = FocusMeter.GetPain(__instance.entityId);
				if (banked > __instance.painResistPercent)
				{
					__instance.painResistPercent = banked;
					Counters.PainRestored++;
				}
			}

			float meter = FocusMeter.Get(__instance.entityId);
			if (meter < Settings.BreakPoints || _dmResponse.Fatal)
			{
				return;
			}
			DamageSource source = _dmResponse.Source;
			if (source == null || source.BuffClass != null || HitClassifier.Attacker(__instance, source) == null)
			{
				return;
			}

			TurnSideways(ref _dmResponse, meter);
			TryKneelSideways(__instance, ref _dmResponse, meter);
		}

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
			// place, so there is nothing here to hold down and nothing to bank.
			if (Settings.NeutralizePainMeter && __instance.painResistPercent > Ceiling && IsBroken(__instance))
			{
				FocusMeter.SetPain(__instance.entityId, __instance.painResistPercent);
				__instance.painResistPercent = Ceiling;
				Counters.PainClamped++;
			}
			else
			{
				// Not clamped, so the enemy's own field is the truth again.
				FocusMeter.SetPain(__instance.entityId, 0f);
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
		/// A hit from the front or the back becomes a hit from the left or the right. The game
		/// works the direction out from where the hit came from (<c>EntityAlive.cs:4598</c>) and
		/// reads it for the flinch and for an animated knockdown or kneel; nothing else reads it -
		/// the ragdoll impulse and the dismember use the damage source's own vector.
		/// </summary>
		private static void TurnSideways(ref DamageResponse _dmResponse, float _meter)
		{
			if (Settings.SidePercent <= 0f)
			{
				return;
			}
			if (_dmResponse.HitDirection != Utils.EnumHitDirection.Front
				&& _dmResponse.HitDirection != Utils.EnumHitDirection.Back)
			{
				return;
			}
			if (_dmResponse.Random >= _meter * Settings.SidePercent / 100f)
			{
				return;
			}

			_dmResponse.HitDirection = SidePick(_dmResponse.Random);
			Counters.SideFlinches++;
		}

		/// <summary>
		/// A hit that would only flinch drops the enemy to a knee, sideways. The response is filled
		/// in the way the game fills in a leg knockdown (<c>EntityAlive.cs:4680-4682</c>): the stun,
		/// its length from the class's own kneel range, and the side. The body then plays it as it
		/// plays any kneel - the animated drop, or the game's own one-in-four ragdoll instead. The
		/// gates are the ones the game applies before it computes a knockdown, plus the ones
		/// Stumblr checks before a trip: nothing to play on a crawler, a sleeper or an enemy
		/// already down.
		/// </summary>
		private static void TryKneelSideways(EntityAlive _entity, ref DamageResponse _dmResponse, float _meter)
		{
			if (Settings.SideFallPercent <= 0f || !_dmResponse.PainHit
				|| _dmResponse.Stun != EnumEntityStunType.None || !_dmResponse.Source.CanStun)
			{
				return;
			}
			if (_entity.bodyDamage.CurrentStun != EnumEntityStunType.None || _entity.sleepingOrWakingUp
				|| _entity.walkType == 21 || _entity.IsDead()
				|| _entity.emodel == null || _entity.emodel.avatarController == null)
			{
				return;
			}
			if (Fraction(_dmResponse.Random * 31f) >= _meter * Settings.SideFallPercent / 100f)
			{
				return;
			}

			_dmResponse.Stun = EnumEntityStunType.Kneel;
			_dmResponse.StunDuration = KneelSeconds(_entity);
			_dmResponse.HitDirection = SidePick(_dmResponse.Random);
			Counters.SideFalls++;
		}

		/// <summary>Left or right off the hit's own random number, so every machine picks the same side.</summary>
		private static Utils.EnumHitDirection SidePick(float _random)
		{
			return Fraction(_random * 97f) < 0.5f ? Utils.EnumHitDirection.Left : Utils.EnumHitDirection.Right;
		}

		private static float Fraction(float _value)
		{
			return _value - (float)System.Math.Floor(_value);
		}

		/// <summary>
		/// The enemy's own kneel range from entityclasses.xml, 0.5 to 1.8 seconds for the vanilla
		/// template, rolled the way the game rolls it. A class with no range set gets one second.
		/// </summary>
		private static float KneelSeconds(EntityAlive _entity)
		{
			UnityEngine.Vector2 range = EntityClass.list[_entity.entityClass].KnockdownKneelStunDuration;
			if (range.y <= 0f)
			{
				return 1f;
			}
			return _entity.rand.RandomRange(range.x, range.y);
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

			// The body has just set BeginStunTrigger for the animated fall. The ragdoll disables the
			// animator before it can consume it, and a trigger left latched replays the fall when the
			// ragdoll ends - with the stun already Getup, nothing ever ends that second fall.
			AvatarController avatar = _entity.emodel.avatarController;
			if (avatar != null)
			{
				avatar._resetTrigger(AvatarController.beginStunTriggerHash);
			}

			_entity.DoRagdoll(_dmResponse);
			Counters.RagdollsForced++;
		}
	}
}
