using UnityEngine;

namespace WhackLash
{
	/// <summary>
	/// Where an enemy stands up after a ragdoll. <c>Entity.PhysicsResume</c> has one caller,
	/// <c>EModelBase.BlendRagdoll</c> at <c>:1352-1374</c>, which finds the floor by casting a ray
	/// <em>down from the pelvis</em>. A ragdoll whose pelvis was driven through the terrain or a
	/// floor starts that ray below the surface, misses it, and the body's collider is switched back
	/// on where the pelvis lies: waist-deep in the ground, unable to move. The game pushes only the
	/// local player out of blocks, so nothing ever frees it.
	///
	/// This mod makes more ragdolls, and harder ones - the damage bonus feeds the ragdoll's impulse
	/// (<c>EModelBase.DoRagdoll</c>, <c>:985-1021</c>) - so it meets that more often than the game
	/// alone does. The prefix looks again from above: a surface between head height and the feet,
	/// straight over the spot the game chose, means the spot is under it, and the enemy is stood on
	/// that surface instead. A standing enemy has nothing over its own feet below that height, so a
	/// spot the game got right is never moved.
	/// </summary>
	internal static class StandUpPatches
	{
		/// <summary>How far above the chosen spot the second look starts.</summary>
		private const float LookHeight = 1.2f;

		/// <summary>A surface this little above the spot is the floor the game already found.</summary>
		private const float Tolerance = 0.1f;

		/// <summary>The mask the game's own stand-up ray uses.</summary>
		private const int Mask = -538750981;

		internal static void PhysicsResumePrefix(Entity __instance, ref Vector3 pos)
		{
			if (!Settings.Enabled || !(__instance is EntityAlive) || __instance is EntityPlayer)
			{
				return;
			}

			Vector3 origin = pos - Origin.position;
			float feet = origin.y;
			origin.y += LookHeight;
			// The enemy's own colliders are already back on; step through them as the game does.
			for (int i = 0; i < 5; i++)
			{
				float reach = origin.y - feet - Tolerance;
				if (reach <= 0f || !Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach, Mask))
				{
					return;
				}
				RootTransformRefEntity owner = hit.transform.GetComponent<RootTransformRefEntity>();
				if (!owner || owner.RootTransform != __instance.transform)
				{
					pos.y = hit.point.y + 0.02f + Origin.position.y;
					Counters.StandUpsLifted++;
					return;
				}
				origin.y = hit.point.y - 0.01f;
			}
		}
	}
}
