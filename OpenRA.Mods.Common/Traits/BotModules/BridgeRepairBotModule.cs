#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Manages AI repairing bridges when they are destroyed and block critical paths.")]
	public class BridgeRepairBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Minimum delay (in ticks) between bridge repair checks.")]
		public readonly int CheckInterval = 150;

		[ActorReference]
		[Desc("Actor types that can repair bridges (must have RepairsBridges trait).")]
		public readonly ImmutableArray<string> RepairActorTypes = ["e6"];

		[Desc("Minimum cash required to train a repair unit.")]
		public readonly int MinimumCashForProduction = 300;

		[Desc("Maximum number of repair units to request per bridge.")]
		public readonly int MaxRepairUnitsPerBridge = 2;

		[Desc("Maximum distance (in cells) to search for destroyed bridges from AI base.")]
		public readonly int MaxSearchDistance = 50;

		public override object Create(ActorInitializer init) { return new BridgeRepairBotModule(init.Self, this); }
	}

	public class BridgeRepairBotModule : ConditionalTrait<BridgeRepairBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		readonly HashSet<Actor> assignedBridges = [];
		readonly Dictionary<Actor, int> bridgeRepairAttempts = [];

		int ticksSinceLastCheck;
		BridgeLayer bridgeLayer;
		PlayerResources playerResources;

		public BridgeRepairBotModule(Actor self, BridgeRepairBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			bridgeLayer = world.WorldActor.TraitOrDefault<BridgeLayer>();
			playerResources = player.PlayerActor.Trait<PlayerResources>();
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (IsTraitDisabled || bridgeLayer == null)
				return;

			if (--ticksSinceLastCheck <= 0)
			{
				ticksSinceLastCheck = Info.CheckInterval;
				CheckAndRepairBridges(bot);
			}
		}

		void CheckAndRepairBridges(IBot bot)
		{
			// Find all destroyed bridge huts
			var destroyedBridges = world.Actors
				.Where(a => a.IsInWorld && !a.IsDead)
				.Select(a => new { Actor = a, Hut = a.TraitOrDefault<BridgeHut>() })
				.Where(x => x.Hut != null && x.Hut.BridgeDamageState == DamageState.Dead && !x.Hut.Repairing)
				.Select(x => x.Actor)
				.ToList();

			// Also check legacy bridge huts
			var destroyedLegacyBridges = world.Actors
				.Where(a => a.IsInWorld && !a.IsDead)
				.Select(a => new { Actor = a, Hut = a.TraitOrDefault<LegacyBridgeHut>() })
				.Where(x => x.Hut != null && x.Hut.BridgeDamageState == DamageState.Dead && !x.Hut.Repairing)
				.Select(x => x.Actor)
				.ToList();

			var allDestroyedBridges = destroyedBridges.Concat(destroyedLegacyBridges).ToList();

			// Remove bridges that have been repaired from tracking
			assignedBridges.RemoveWhere(b => b.IsDead || !allDestroyedBridges.Contains(b));
			var keysToRemove = bridgeRepairAttempts.Keys.Where(k => k.IsDead || !allDestroyedBridges.Contains(k)).ToList();
			foreach (var key in keysToRemove)
				bridgeRepairAttempts.Remove(key);

			// Check each destroyed bridge
			foreach (var bridge in allDestroyedBridges)
			{
				if (assignedBridges.Contains(bridge))
					continue;

				// Check if bridge is critical (near AI base and potentially blocking paths)
				if (IsBridgeCritical(bridge))
				{
					AIUtils.BotDebug("{0} detected destroyed critical bridge at {1}", player, bridge.Location);
					AttemptBridgeRepair(bot, bridge);
				}
			}
		}

		bool IsBridgeCritical(Actor bridge)
		{
			// Find AI base buildings
			var baseBuildings = world.Actors
				.Where(a => a.Owner == player && !a.IsDead && a.Info.HasTraitInfo<BuildingInfo>())
				.ToList();

			if (baseBuildings.Count == 0)
				return false;

			// Check if bridge is within search distance from any base building
			var nearestBaseDistance = baseBuildings
				.Min(b => (b.Location - bridge.Location).Length);

			if (nearestBaseDistance > Info.MaxSearchDistance)
				return false;

			// Consider bridges critical if we have harvesters (they might need to cross)
			var hasHarvesters = world.ActorsWithTrait<Harvester>()
				.Any(a => a.Actor.Owner == player && !a.Actor.IsDead);

			if (hasHarvesters)
				return true;

			// Check if bridge is near visible enemy structures
			var hasNearbyEnemies = world.Actors
				.Any(a => a.Owner.RelationshipWith(player) == PlayerRelationship.Enemy &&
						!a.IsDead &&
						a.Info.HasTraitInfo<BuildingInfo>() &&
						player.Shroud.IsExplored(a.Location) &&
						(a.Location - bridge.Location).Length <= Info.MaxSearchDistance);

			return hasNearbyEnemies;
		}

		void AttemptBridgeRepair(IBot bot, Actor bridge)
		{
			// Find idle repair units
			var repairUnits = world.ActorsWithTrait<RepairsBridges>()
				.Where(a => a.Actor.Owner == player &&
							!a.Actor.IsDead &&
							a.Actor.IsIdle)
				.Select(a => a.Actor)
				.ToList();

			// Assign idle repair unit to bridge
			if (repairUnits.Count > 0)
			{
				var repairUnit = repairUnits[0];
				AIUtils.BotDebug("{0} sending {1} to repair bridge at {2}", player, repairUnit, bridge.Location);

				bot.QueueOrder(new Order("RepairBridge", repairUnit, Target.FromActor(bridge), false));
				assignedBridges.Add(bridge);
				return;
			}

			// Check if we've already requested too many engineers for this bridge
			if (!bridgeRepairAttempts.TryGetValue(bridge, out var attempts))
			{
				attempts = 0;
				bridgeRepairAttempts[bridge] = attempts;
			}

			if (attempts >= Info.MaxRepairUnitsPerBridge)
				return;

			// Check if we have enough cash to train repair unit
			if (playerResources.GetCashAndResources() < Info.MinimumCashForProduction)
				return;

			// Request production of repair unit
			var requestProduction = player.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>()
				.FirstOrDefault();

			if (requestProduction != null)
			{
				foreach (var repairActorType in Info.RepairActorTypes)
				{
					if (!world.Map.Rules.Actors.ContainsKey(repairActorType))
						continue;

					var actorInfo = world.Map.Rules.Actors[repairActorType];
					if (actorInfo.HasTraitInfo<RepairsBridgesInfo>())
					{
						AIUtils.BotDebug("{0} requesting {1} to repair bridge at {2}", player, repairActorType, bridge.Location);
						requestProduction.RequestUnitProduction(bot, repairActorType);
						bridgeRepairAttempts[bridge] = attempts + 1;
						assignedBridges.Add(bridge);
						break;
					}
				}
			}
		}
	}
}
