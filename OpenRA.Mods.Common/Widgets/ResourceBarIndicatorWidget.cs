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

using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	// Draws the indicator sprite of a sibling ResourceBarWidget. Use this to render the
	// indicator on top of overlay widgets (e.g. striped power bars) that would otherwise
	// be drawn after the ResourceBar and hide the indicator.
	public class ResourceBarIndicatorWidget : Widget
	{
		public string ResourceBar = "POWERBAR";

		ResourceBarWidget resourceBar;

		public ResourceBarIndicatorWidget() { }

		protected ResourceBarIndicatorWidget(ResourceBarIndicatorWidget widget)
			: base(widget)
		{
			ResourceBar = widget.ResourceBar;
		}

		public override ResourceBarIndicatorWidget Clone() { return new ResourceBarIndicatorWidget(this); }

		public override void Draw()
		{
			// Track the parent's current size so the indicator follows when the parent is resized
			// after init (e.g. ClassicProductionLogic adjusts POWER_BAR_PANEL.Bounds.Height).
			if (Parent != null)
			{
				Bounds.Width = Parent.Bounds.Width;
				Bounds.Height = Parent.Bounds.Height;
			}

			resourceBar ??= Parent?.GetOrNull<ResourceBarWidget>(ResourceBar);
			if (resourceBar == null || resourceBar.IndicatorSprite == null)
				return;

			var sprite = resourceBar.IndicatorSprite;
			var b = RenderBounds;
			var usedFrac = resourceBar.LastUsedFrac;

			if (resourceBar.Orientation == ResourceBarOrientation.Vertical)
			{
				var x = (b.Left + b.Right - sprite.Size.X) / 2;
				var y = float2.Lerp(b.Bottom, b.Top, usedFrac) - sprite.Size.Y / 2;
				WidgetUtils.DrawSprite(sprite, new float2(x, y));
			}
			else
			{
				var x = float2.Lerp(b.Left, b.Right, usedFrac) - sprite.Size.X / 2;
				var y = (b.Bottom + b.Top - sprite.Size.Y) / 2;
				WidgetUtils.DrawSprite(sprite, new float2(x, y));
			}
		}
	}
}
