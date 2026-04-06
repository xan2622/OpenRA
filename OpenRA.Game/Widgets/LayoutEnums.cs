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

namespace OpenRA.Widgets
{
	public enum FlexDirection
	{
		Row,
		Column
	}

	public enum JustifyContent
	{
		Start,
		End,
		Center,
		SpaceBetween,
		SpaceAround,
		SpaceEvenly
	}

	public enum AlignItems
	{
		Start,
		End,
		Center,
		Stretch
	}

	public enum AlignSelf
	{
		Auto,
		Start,
		End,
		Center,
		Stretch
	}

	public enum WidgetLayout
	{
		Absolute,
		Flex,
		Fixed
	}

	public enum FlexWrap
	{
		NoWrap,
		Wrap
	}

	public enum OverflowMode
	{
		Visible,
		Hidden,
		Scroll
	}

	public enum SizingMode
	{
		Fixed,
		FitContent,
		Fill
	}

	public enum AlignContent
	{
		Start,
		End,
		Center,
		SpaceBetween,
		SpaceAround,
		SpaceEvenly,
		Stretch
	}
}
