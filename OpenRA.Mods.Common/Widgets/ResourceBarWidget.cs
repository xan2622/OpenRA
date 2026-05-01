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
	public enum ResourceBarOrientation { Vertical, Horizontal }
	public class ResourceBarWidget : Widget
	{
		public readonly string TooltipTemplate;
		public readonly string TooltipContainer;
		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		public CachedTransform<(float, float), string> TooltipTextCached;
		public ResourceBarOrientation Orientation = ResourceBarOrientation.Vertical;
		public string IndicatorCollection = "sidebar-bits";
		public string IndicatorImage = "indicator";

		// Procedural indicator (used when IndicatorImage is empty): a solid rectangle
		// drawn at the drained-power position. Faithful to REDALERT/POWER.CPP, the
		// marker is always drawn -- including when provided power is 0 (drain_height = 0
		// just pins it at the bottom of the bar).
		public int IndicatorWidth = 0;
		public int IndicatorHeight = 0;
		public int IndicatorOffsetX = 0;
		public Color IndicatorColor = Color.White;

		// When true, the bar does not draw its own indicator sprite. Use this with
		// a sibling ResourceBarIndicatorWidget placed after any overlay widgets so the
		// indicator can be drawn on top of them.
		public bool SuppressIndicator = false;

		public Func<float> GetProvided = () => 0;
		public Func<float> GetUsed = () => 0;
		public Func<Color> GetBarColor = () => Color.White;
		readonly EWMA providedLerp = new(0.3f);
		readonly EWMA usedLerp = new(0.3f);
		readonly World world;

		public Sprite IndicatorSprite { get; private set; }
		public float LastUsedFrac { get; private set; }

		[ObjectCreator.UseCtor]
		public ResourceBarWidget(World world)
		{
			this.world = world;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			if (!string.IsNullOrEmpty(IndicatorImage))
				IndicatorSprite = ChromeProvider.GetImage(IndicatorCollection, IndicatorImage);
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
			var scaleBy = 100.0f;
			var provided = GetProvided();
			var used = GetUsed();
			var max = Math.Max(provided, used);
			while (max >= scaleBy)
				scaleBy *= 2;

			var providedFrac = providedLerp.Update(provided / scaleBy);
			var usedFrac = usedLerp.Update(used / scaleBy);
			LastUsedFrac = usedFrac;

			var b = RenderBounds;

			var color = GetBarColor();
			if (Orientation == ResourceBarOrientation.Vertical)
			{
				var tl = new float2(b.X, (int)float2.Lerp(b.Bottom, b.Top, providedFrac));
				var br = new float2(b.X + b.Width, b.Bottom);
				Game.Renderer.RgbaColorRenderer.FillRect(tl, br, color);

				if (!SuppressIndicator)
				{
					if (IndicatorSprite != null)
					{
						var x = (b.Left + b.Right - IndicatorSprite.Size.X) / 2;
						var y = float2.Lerp(b.Bottom, b.Top, usedFrac) - IndicatorSprite.Size.Y / 2;
						WidgetUtils.DrawSprite(IndicatorSprite, new float2(x, y));
					}
					else if (IndicatorWidth > 0 && IndicatorHeight > 0)
					{
						var x = b.Left + (b.Width - IndicatorWidth) / 2 + IndicatorOffsetX;
						var y = (int)float2.Lerp(b.Bottom, b.Top, usedFrac) - IndicatorHeight / 2;
						Game.Renderer.RgbaColorRenderer.FillRect(
							new float2(x, y),
							new float2(x + IndicatorWidth, y + IndicatorHeight),
							IndicatorColor);
					}
				}
			}
			else
			{
				var tl = new float2(b.X, b.Y);
				var br = tl + new float2((int)(providedFrac * b.Width), b.Height);
				Game.Renderer.RgbaColorRenderer.FillRect(tl, br, color);

				if (!SuppressIndicator)
				{
					if (IndicatorSprite != null)
					{
						var x = float2.Lerp(b.Left, b.Right, usedFrac) - IndicatorSprite.Size.X / 2;
						var y = (b.Bottom + b.Top - IndicatorSprite.Size.Y) / 2;
						WidgetUtils.DrawSprite(IndicatorSprite, new float2(x, y));
					}
					else if (IndicatorWidth > 0 && IndicatorHeight > 0)
					{
						var x = (int)float2.Lerp(b.Left, b.Right, usedFrac) - IndicatorWidth / 2;
						var y = b.Top + (b.Height - IndicatorHeight) / 2;
						Game.Renderer.RgbaColorRenderer.FillRect(
							new float2(x, y),
							new float2(x + IndicatorWidth, y + IndicatorHeight),
							IndicatorColor);
					}
				}
			}
		}
	}
}
