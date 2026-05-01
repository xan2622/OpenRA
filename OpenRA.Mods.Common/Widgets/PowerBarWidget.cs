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

using System;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	// Vertical power bar inspired by the Westwood TIBERIANDAWN/POWER.CPP design:
	// the bar fills from the bottom up with small pre-rendered segment sprites
	// (e.g. 10x2 green/red rectangles from chrome.png). The lower portion (height
	// proportional to drained power) is drawn with the red segment, the upper
	// portion (excess = provided - drained) is drawn with the green segment.
	// When drained > provided the whole filled area is drawn with the red segment.
	//
	// The topmost segment flashes (white overlay sprite) whenever the bar level
	// changes, for the duration of the movement plus a minimum trailing window
	// (~1 s), reproducing the Tiberian Sun original bounce/flicker behavior.
	// In TIBERIANDAWN/POWER.CPP, the equivalent effect was the procedural bounce
	// driven by _modtable + PowerBounce/DrainBounce (12 ticks ~= 800 ms at 15 FPS).
	public class PowerBarWidget : Widget
	{
		public readonly string TooltipTemplate;
		public readonly string TooltipContainer;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		public CachedTransform<(float, float), string> TooltipTextCached;

		public Func<float> GetProvided = () => 0;
		public Func<float> GetUsed = () => 0;

		// Image collection containing the segment sprites (one green, one red, one flash).
		// AddFactionSuffixLogic appends "-gdi" / "-nod" at runtime.
		public string ImageCollection = "";
		public string GreenSegment = "powerbar-green";
		public string RedSegment = "powerbar-red";
		public string FlashSegment = "powerbar-flash";

		// Optional permanent indicator sprite drawn at the topmost filled slot.
		// Unlike FlashSegment it is always visible (no blinking). Use IndicatorOffsetX
		// to shift it left so it overlaps the bar border.
		public string IndicatorImage = "";
		public int IndicatorOffsetX = 0;

		// Vertical gap in pixels left between two stacked segment sprites, used to
		// reproduce the notched appearance of the original power bar.
		public int SegmentSpacing = 0;

		// Minimum number of world ticks the flash effect stays armed after the bar
		// stops moving. At OpenRA's 25 ticks/s, 25 ticks = ~1 s, matching the
		// "minimum of approximately one second" observed in TS.
		public int FlashMinTicks = 25;

		// World ticks between flash on/off toggles.
		public int FlashIntervalTicks = 2;

		readonly EWMA providedLerp = new(0.3f);
		readonly EWMA usedLerp = new(0.3f);
		readonly World world;

		readonly CachedTransform<(string, string), Sprite> getImageCache = new(
			((string Collection, string Image) args) => ChromeProvider.GetImage(args.Collection, args.Image));

		// Last provided/used values sampled per world tick (not per render frame), used
		// to detect bar movement just like PowerClass::AI compares against RecordedPower.
		int lastSampledTick = int.MinValue;
		float lastSampledProvided;
		float lastSampledUsed;
		int flashUntilTick;

		public PowerBarWidget() { }

		[ObjectCreator.UseCtor]
		public PowerBarWidget(World world)
		{
			this.world = world;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override void MouseEntered()
		{
			if (TooltipContainer == null)
				return;

			Func<string> getText = () => TooltipTextCached.Update((GetUsed(), GetProvided()));
			tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs() { { "getText", getText }, { "world", world } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer == null)
				return;
			tooltipContainer.Value.RemoveTooltip();
		}

		public override void Draw()
		{
			// Track parent's current size so the bar follows when the parent is resized
			// after init (e.g. ClassicProductionLogic adjusts POWER_BAR_PANEL.Bounds.Height).
			if (Parent != null)
			{
				Bounds.Width = Parent.Bounds.Width;
				Bounds.Height = Parent.Bounds.Height;
			}

			var green = getImageCache.Update((ImageCollection, GreenSegment));
			var red = getImageCache.Update((ImageCollection, RedSegment));
			if (green == null || red == null)
				return;

			var segmentHeight = (int)green.Size.Y;
			if (segmentHeight <= 0)
				return;

			var b = RenderBounds;

			// Each stacked cell takes the sprite height plus the configured gap.
			var slotHeight = segmentHeight + Math.Max(0, SegmentSpacing);

			// Total number of segment slots that fit in the bar height. This is the
			// equivalent of PowHeight in the original POWER.CPP.
			var totalSlots = b.Height / slotHeight;
			if (totalSlots <= 0)
				return;

			var scaleBy = 100.0f;
			var provided = GetProvided();
			var used = GetUsed();
			var max = Math.Max(provided, used);
			while (max >= scaleBy)
				scaleBy *= 2;

			// Sample the target values once per world tick (analogous to PowerClass::AI
			// running once per game tick). Arm the flash whenever the target changes,
			// in either direction.
			var worldTick = world?.WorldTick ?? 0;
			if (worldTick != lastSampledTick)
			{
				if (lastSampledTick != int.MinValue &&
					(provided != lastSampledProvided || used != lastSampledUsed))
				{
					flashUntilTick = worldTick + FlashMinTicks;
				}

				lastSampledProvided = provided;
				lastSampledUsed = used;
				lastSampledTick = worldTick;
			}

			var providedTargetFrac = Math.Max(0f, provided) / scaleBy;
			var usedTargetFrac = Math.Max(0f, used) / scaleBy;
			var providedFrac = providedLerp.Update(providedTargetFrac);
			var usedFrac = usedLerp.Update(usedTargetFrac);

			// The lerp keeps approaching the target asymptotically: treat anything
			// closer than half a slot as "stopped" to avoid an endless flash.
			var halfSlotFrac = 0.5f / totalSlots;
			var stillMoving = Math.Abs(providedFrac - providedTargetFrac) > halfSlotFrac
				|| Math.Abs(usedFrac - usedTargetFrac) > halfSlotFrac;

			// Critical state: drain exceeds provided power -> whole filled area is red,
			// mirroring TD's power_color = 2/4 branch that swaps the fill frames.
			var drainOverProvided = used > provided && provided > 0;

			// Number of segments to draw, capped at totalSlots.
			// Mirrors TD's Bound(power_height, 0, PowHeight - 2).
			var providedSlots = Math.Min(totalSlots, (int)Math.Round(providedFrac * totalSlots));
			var usedSlots = Math.Min(providedSlots, (int)Math.Round(usedFrac * totalSlots));

			int redSlots, greenSlots;
			if (drainOverProvided)
			{
				redSlots = providedSlots;
				greenSlots = 0;
			}
			else
			{
				redSlots = usedSlots;
				greenSlots = providedSlots - usedSlots;
			}

			// Stack segments from the bottom up. The bar is anchored at b.Bottom; each
			// segment occupies one slot of segmentHeight pixels.
			var x = b.X + (b.Width - (int)green.Size.X) / 2;
			var bottom = b.Bottom;

			for (var i = 0; i < redSlots; i++)
			{
				var y = bottom - (i + 1) * slotHeight + (slotHeight - segmentHeight);
				WidgetUtils.DrawSprite(red, new float2(x, y));
			}

			for (var i = 0; i < greenSlots; i++)
			{
				var y = bottom - (redSlots + i + 1) * slotHeight + (slotHeight - segmentHeight);
				WidgetUtils.DrawSprite(green, new float2(x, y));
			}

			// Flash overlay on the topmost filled segment.
			var topSlots = redSlots + greenSlots;
			if (topSlots > 0)
			{
				var flashOn = (worldTick < flashUntilTick) || stillMoving;
				if (flashOn)
				{
					var interval = Math.Max(1, FlashIntervalTicks);
					var blink = worldTick / interval % 2 == 0;
					if (blink)
					{
						var flash = getImageCache.Update((ImageCollection, FlashSegment));
						if (flash != null)
						{
							var topY = bottom - topSlots * slotHeight + (slotHeight - segmentHeight);
							var flashX = b.X + (b.Width - (int)flash.Size.X) / 2;
							var flashY = topY - ((int)flash.Size.Y - segmentHeight) / 2;
							WidgetUtils.DrawSprite(flash, new float2(flashX, flashY));
						}
					}
				}

				// Permanent indicator sprite at the topmost filled slot.
				if (!string.IsNullOrEmpty(IndicatorImage))
				{
					var indicator = getImageCache.Update((ImageCollection, IndicatorImage));
					if (indicator != null)
					{
						var topY = bottom - topSlots * slotHeight + (slotHeight - segmentHeight);
						var indicatorX = b.X + IndicatorOffsetX;
						var indicatorY = topY - ((int)indicator.Size.Y - segmentHeight) / 2;
						WidgetUtils.DrawSprite(indicator, new float2(indicatorX, indicatorY));
					}
				}
			}
		}
	}
}
