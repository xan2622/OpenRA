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
using System.Linq;
using OpenRA.Mods.Common.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Handles spectator beacon effects broadcast over the network.",
		"Requires PlaceBeacon on the player actor to define beacon appearance and duration.")]
	public class SpectatorEffectsInfo : TraitInfo<SpectatorEffects> { }

	public class SpectatorEffects : INotifySpectatorBeacon, ITick
	{
		readonly Dictionary<string, Beacon> spectatorBeacons = [];
		readonly Dictionary<string, RadarPing> spectatorRadarPings = [];
		RadarPings radarPings;

		// Bright yellow is legible over all tilesets (green, sand, snow, grey interior).
		static readonly Color BeaconColor = Color.FromArgb(255, 224, 0);

		void ITick.Tick(Actor self)
		{
			// Remove entries for spectators whose beacons have expired naturally.
			var expiredNames = spectatorBeacons
				.Where(kv => !self.World.UnpartitionedEffects.Contains(kv.Value))
				.Select(kv => kv.Key)
				.ToList();

			foreach (var name in expiredNames)
			{
				spectatorBeacons.Remove(name);
				spectatorRadarPings.Remove(name);
			}
		}

		void INotifySpectatorBeacon.SpectatorBeaconPlaced(World world, WPos position, string spectatorName)
		{
			var player = world.Players.FirstOrDefault(p => p.Spectating && p.NonCombatant);
			if (player == null)
				return;

			var info = player.PlayerActor.Info.TraitInfoOrDefault<PlaceBeaconInfo>();
			if (info == null)
				return;

			radarPings ??= world.WorldActor.TraitOrDefault<RadarPings>();

			world.AddFrameEndTask(w =>
			{
				// Remove the previous beacon for this spectator, like player beacons.
				if (spectatorBeacons.TryGetValue(spectatorName, out var previousBeacon))
				{
					w.Remove(previousBeacon);
					spectatorBeacons.Remove(spectatorName);
				}

				var beacon = new Beacon(player, position, info.Duration,
					"effect", false,
					info.BeaconImage, info.BeaconSequence, info.ArrowSequence, info.CircleSequence,
					spectatorName: spectatorName);

				spectatorBeacons[spectatorName] = beacon;
				w.Add(beacon);

				if (world.RenderPlayer == null || world.RenderPlayer.Spectating)
					Game.Sound.PlayNotification(world.Map.Rules, null, info.NotificationType, info.Notification, null);

				if (radarPings != null)
				{
					if (spectatorRadarPings.TryGetValue(spectatorName, out var previousPing))
					{
						radarPings.Remove(previousPing);
						spectatorRadarPings.Remove(spectatorName);
					}

					spectatorRadarPings[spectatorName] = radarPings.Add(
						() => world.RenderPlayer == null || world.RenderPlayer.Spectating,
						position,
						BeaconColor,
						info.Duration);
				}
			});
		}
	}
}
