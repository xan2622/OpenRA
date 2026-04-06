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
using System.Collections.Generic;

namespace OpenRA.Widgets
{
	public static class FlexLayout
	{
		public static void PerformLayout(Widget container)
		{
			var flowChildren = CollectFlowChildren(container);
			if (flowChildren.Count == 0)
				return;

			if (container.FlexWrap == FlexWrap.Wrap)
				PerformWrappedLayout(container, flowChildren);
			else
				PerformSingleLineLayout(container, flowChildren);
		}

		static void PerformSingleLineLayout(Widget container, List<Widget> flowChildren)
		{
			var isRow = container.FlexDirection == FlexDirection.Row;
			var gap = container.Gap;

			var insetH = container.Padding.Horizontal + container.Border.Horizontal;
			var insetV = container.Padding.Vertical + container.Border.Vertical;

			var availableMain = isRow
				? container.Bounds.Width - insetH
				: container.Bounds.Height - insetV;
			var availableCross = isRow
				? container.Bounds.Height - insetV
				: container.Bounds.Width - insetH;

			var count = flowChildren.Count;
			var bases = new int[count];
			var margins = new int[count];
			var totalBasis = 0;
			var totalMargins = 0;
			var totalGrow = 0f;
			var totalShrink = 0f;

			for (var i = 0; i < count; i++)
			{
				var child = flowChildren[i];
				var basis = isRow ? child.Bounds.Width : child.Bounds.Height;
				var margin = isRow ? child.Margin.Horizontal : child.Margin.Vertical;
				bases[i] = basis;
				margins[i] = margin;
				totalBasis += basis;
				totalMargins += margin;
				totalGrow += child.FlexGrow;
				totalShrink += child.FlexShrink;
			}

			var totalGaps = count > 1 ? (count - 1) * gap : 0;
			var freeSpace = availableMain - totalBasis - totalMargins - totalGaps;
			var sizes = new int[count];

			if (freeSpace > 0 && totalGrow > 0)
				DistributeGrow(sizes, bases, freeSpace, flowChildren, totalGrow);
			else if (freeSpace < 0 && totalShrink > 0)
				DistributeShrink(sizes, bases, freeSpace, flowChildren, totalShrink);
			else
				Array.Copy(bases, sizes, count);

			var positions = CalculateMainPositions(
				container.JustifyContent, availableMain - totalMargins, sizes, gap, count);

			for (var i = 0; i < count; i++)
			{
				var child = flowChildren[i];
				var mainMarginStart = isRow ? child.Margin.Left : child.Margin.Top;
				var crossMarginStart = isRow ? child.Margin.Top : child.Margin.Left;
				var crossMargin = isRow ? child.Margin.Vertical : child.Margin.Horizontal;

				// Apply min/max constraints to main axis size
				sizes[i] = ClampMainSize(child, sizes[i], isRow);

				if (isRow)
				{
					child.Bounds.X = positions[i] + mainMarginStart;
					child.Bounds.Width = sizes[i];
				}
				else
				{
					child.Bounds.Y = positions[i] + mainMarginStart;
					child.Bounds.Height = sizes[i];
				}

				ApplyCrossAlignment(child, container.AlignItems, availableCross - crossMargin, isRow);

				// Offset cross position by margin
				if (isRow)
					child.Bounds.Y += crossMarginStart;
				else
					child.Bounds.X += crossMarginStart;
			}
		}

		static void PerformWrappedLayout(Widget container, List<Widget> flowChildren)
		{
			var isRow = container.FlexDirection == FlexDirection.Row;
			var gap = container.Gap;

			var insetH = container.Padding.Horizontal + container.Border.Horizontal;
			var insetV = container.Padding.Vertical + container.Border.Vertical;

			var availableMain = isRow
				? container.Bounds.Width - insetH
				: container.Bounds.Height - insetV;
			var availableCross = isRow
				? container.Bounds.Height - insetV
				: container.Bounds.Width - insetH;

			// Split children into lines
			var lines = new List<List<Widget>>();
			var currentLine = new List<Widget>();
			var currentLineSize = 0;

			foreach (var child in flowChildren)
			{
				var childMarginMain = isRow ? child.Margin.Horizontal : child.Margin.Vertical;
				var childMain = (isRow ? child.Bounds.Width : child.Bounds.Height) + childMarginMain;
				var gapSize = currentLine.Count > 0 ? gap : 0;

				if (currentLine.Count > 0 && currentLineSize + gapSize + childMain > availableMain)
				{
					lines.Add(currentLine);
					currentLine = [];
					currentLineSize = 0;
					gapSize = 0;
				}

				currentLine.Add(child);
				currentLineSize += gapSize + childMain;
			}

			if (currentLine.Count > 0)
				lines.Add(currentLine);

			// Compute cross size of each line
			var lineCrossSizes = new int[lines.Count];
			for (var l = 0; l < lines.Count; l++)
			{
				for (var i = 0; i < lines[l].Count; i++)
				{
					var childCross = isRow ? lines[l][i].Bounds.Height : lines[l][i].Bounds.Width;
					var crossMargin = isRow ? lines[l][i].Margin.Vertical : lines[l][i].Margin.Horizontal;
					lineCrossSizes[l] = Math.Max(lineCrossSizes[l], childCross + crossMargin);
				}
			}

			// Compute line cross offsets using AlignContent
			var crossOffsets = CalculateCrossOffsets(
				container.AlignContent, availableCross, lineCrossSizes, gap);

			// Stretch lines when AlignContent is Stretch
			if (container.AlignContent == AlignContent.Stretch && lines.Count > 0)
			{
				var totalGaps = lines.Count > 1 ? (lines.Count - 1) * gap : 0;
				var extra = availableCross - totalGaps;
				for (var l = 0; l < lines.Count; l++)
					extra -= lineCrossSizes[l];

				if (extra > 0)
				{
					var perLine = extra / lines.Count;
					var remainder = extra % lines.Count;
					for (var l = 0; l < lines.Count; l++)
					{
						lineCrossSizes[l] += perLine + (l < remainder ? 1 : 0);
						crossOffsets[l] = l == 0 ? 0 : crossOffsets[l - 1] + lineCrossSizes[l - 1] + gap;
					}
				}
			}

			// Layout each line
			for (var l = 0; l < lines.Count; l++)
			{
				var line = lines[l];
				var count = line.Count;
				var bases = new int[count];
				var totalBasis = 0;
				var totalLineMargins = 0;

				for (var i = 0; i < count; i++)
				{
					var basis = isRow ? line[i].Bounds.Width : line[i].Bounds.Height;
					var margin = isRow ? line[i].Margin.Horizontal : line[i].Margin.Vertical;
					bases[i] = basis;
					totalBasis += basis;
					totalLineMargins += margin;
				}

				var totalGaps = count > 1 ? (count - 1) * gap : 0;
				var freeSpace = availableMain - totalBasis - totalLineMargins - totalGaps;
				var sizes = new int[count];
				var totalGrow = 0f;
				var totalShrink = 0f;

				for (var i = 0; i < count; i++)
				{
					totalGrow += line[i].FlexGrow;
					totalShrink += line[i].FlexShrink;
				}

				if (freeSpace > 0 && totalGrow > 0)
					DistributeGrow(sizes, bases, freeSpace, line, totalGrow);
				else if (freeSpace < 0 && totalShrink > 0)
					DistributeShrink(sizes, bases, freeSpace, line, totalShrink);
				else
					Array.Copy(bases, sizes, count);

				var positions = CalculateMainPositions(
					container.JustifyContent, availableMain - totalLineMargins, sizes, gap, count);

				var lineCrossSize = lineCrossSizes[l];
				var crossOffset = crossOffsets[l];

				for (var i = 0; i < count; i++)
				{
					var child = line[i];
					var mainMarginStart = isRow ? child.Margin.Left : child.Margin.Top;
					var crossMarginStart = isRow ? child.Margin.Top : child.Margin.Left;
					var crossMargin = isRow ? child.Margin.Vertical : child.Margin.Horizontal;

					if (isRow)
					{
						child.Bounds.X = positions[i] + mainMarginStart;
						child.Bounds.Width = sizes[i];
					}
					else
					{
						child.Bounds.Y = positions[i] + mainMarginStart;
						child.Bounds.Height = sizes[i];
					}

					ApplyCrossAlignment(child, container.AlignItems, lineCrossSize - crossMargin, isRow);

					if (isRow)
						child.Bounds.Y += crossOffset + crossMarginStart;
					else
						child.Bounds.X += crossOffset + crossMarginStart;
				}
			}
		}

		static int[] CalculateCrossOffsets(AlignContent align, int available, int[] lineSizes, int gap)
		{
			var count = lineSizes.Length;
			var offsets = new int[count];

			var totalSizes = 0;
			for (var i = 0; i < count; i++)
				totalSizes += lineSizes[i];

			var totalGaps = count > 1 ? (count - 1) * gap : 0;
			var remaining = available - totalSizes - totalGaps;

			switch (align)
			{
				case AlignContent.Start:
				case AlignContent.Stretch:
				{
					var pos = 0;
					for (var i = 0; i < count; i++)
					{
						offsets[i] = pos;
						pos += lineSizes[i] + gap;
					}

					break;
				}

				case AlignContent.End:
				{
					var pos = remaining;
					for (var i = 0; i < count; i++)
					{
						offsets[i] = pos;
						pos += lineSizes[i] + gap;
					}

					break;
				}

				case AlignContent.Center:
				{
					var pos = remaining / 2;
					for (var i = 0; i < count; i++)
					{
						offsets[i] = pos;
						pos += lineSizes[i] + gap;
					}

					break;
				}

				case AlignContent.SpaceBetween:
				{
					var space = count > 1 ? (float)(available - totalSizes) / (count - 1) : 0;
					var pos = 0f;
					for (var i = 0; i < count; i++)
					{
						offsets[i] = (int)pos;
						pos += lineSizes[i] + space;
					}

					break;
				}

				case AlignContent.SpaceAround:
				{
					var space = count > 0 ? (float)(available - totalSizes) / count : 0;
					var pos = space / 2;
					for (var i = 0; i < count; i++)
					{
						offsets[i] = (int)pos;
						pos += lineSizes[i] + space;
					}

					break;
				}

				case AlignContent.SpaceEvenly:
				{
					var space = count > 0 ? (float)(available - totalSizes) / (count + 1) : 0;
					var pos = space;
					for (var i = 0; i < count; i++)
					{
						offsets[i] = (int)pos;
						pos += lineSizes[i] + space;
					}

					break;
				}
			}

			return offsets;
		}

		public static (int Width, int Height) CalculateIntrinsicSize(Widget container)
		{
			var isRow = container.FlexDirection == FlexDirection.Row;
			var gap = container.Gap;

			if (container.FlexWrap == FlexWrap.Wrap)
				return CalculateWrappedIntrinsicSize(container, isRow, gap);

			var mainSize = 0;
			var crossSize = 0;
			var childCount = 0;

			foreach (var child in container.Children)
			{
				if (!child.IsVisible() || child.Positioning != WidgetLayout.Flex)
					continue;

				var childMain = (isRow ? child.Bounds.Width : child.Bounds.Height)
					+ (isRow ? child.Margin.Horizontal : child.Margin.Vertical);
				var childCross = (isRow ? child.Bounds.Height : child.Bounds.Width)
					+ (isRow ? child.Margin.Vertical : child.Margin.Horizontal);

				mainSize += childMain;
				crossSize = Math.Max(crossSize, childCross);
				childCount++;
			}

			if (childCount > 1)
				mainSize += (childCount - 1) * gap;

			var width = (isRow ? mainSize : crossSize) + container.Padding.Horizontal + container.Border.Horizontal;
			var height = (isRow ? crossSize : mainSize) + container.Padding.Vertical + container.Border.Vertical;

			return (width, height);
		}

		static (int Width, int Height) CalculateWrappedIntrinsicSize(Widget container, bool isRow, int gap)
		{
			var insetH = container.Padding.Horizontal + container.Border.Horizontal;
			var insetV = container.Padding.Vertical + container.Border.Vertical;

			var availableMain = isRow
				? container.Bounds.Width - insetH
				: container.Bounds.Height - insetV;

			var maxLineMain = 0;
			var totalCross = 0;
			var lineMain = 0;
			var lineCross = 0;
			var lineCount = 0;
			var childInLine = 0;

			foreach (var child in container.Children)
			{
				if (!child.IsVisible() || child.Positioning != WidgetLayout.Flex)
					continue;

				var childMain = isRow ? child.Bounds.Width : child.Bounds.Height;
				var childCross = isRow ? child.Bounds.Height : child.Bounds.Width;
				var gapSize = childInLine > 0 ? gap : 0;

				if (childInLine > 0 && lineMain + gapSize + childMain > availableMain)
				{
					maxLineMain = Math.Max(maxLineMain, lineMain);
					totalCross += lineCross + (lineCount > 0 ? gap : 0);
					lineMain = 0;
					lineCross = 0;
					childInLine = 0;
					lineCount++;
					gapSize = 0;
				}

				lineMain += gapSize + childMain;
				lineCross = Math.Max(lineCross, childCross);
				childInLine++;
			}

			if (childInLine > 0)
			{
				maxLineMain = Math.Max(maxLineMain, lineMain);
				totalCross += lineCross + (lineCount > 0 ? gap : 0);
			}

			var width = (isRow ? maxLineMain : totalCross) + container.Padding.Horizontal + container.Border.Horizontal;
			var height = (isRow ? totalCross : maxLineMain) + container.Padding.Vertical + container.Border.Vertical;

			return (width, height);
		}

		static List<Widget> CollectFlowChildren(Widget container)
		{
			var result = new List<Widget>();
			foreach (var child in container.Children)
				if (child.IsVisible() && child.Positioning == WidgetLayout.Flex)
					result.Add(child);
			return result;
		}

		static void DistributeGrow(int[] sizes, int[] bases, int freeSpace,
			List<Widget> children, float totalGrow)
		{
			var distributed = 0;
			for (var i = 0; i < children.Count; i++)
			{
				var extra = (int)(freeSpace * children[i].FlexGrow / totalGrow);
				sizes[i] = bases[i] + extra;
				distributed += extra;
			}

			// Distribute rounding remainder to first items with FlexGrow > 0
			var remainder = freeSpace - distributed;
			for (var i = 0; i < children.Count && remainder > 0; i++)
			{
				if (children[i].FlexGrow > 0)
				{
					sizes[i]++;
					remainder--;
				}
			}
		}

		static void DistributeShrink(int[] sizes, int[] bases, int freeSpace,
			List<Widget> children, float totalShrink)
		{
			var overflow = -freeSpace;
			var distributed = 0;
			for (var i = 0; i < children.Count; i++)
			{
				var shrink = (int)(overflow * children[i].FlexShrink / totalShrink);
				sizes[i] = Math.Max(0, bases[i] - shrink);
				distributed += shrink;
			}

			// Distribute rounding remainder to first items with FlexShrink > 0
			var remainder = overflow - distributed;
			for (var i = 0; i < children.Count && remainder > 0; i++)
			{
				if (children[i].FlexShrink > 0 && sizes[i] > 0)
				{
					sizes[i]--;
					remainder--;
				}
			}
		}

		static int[] CalculateMainPositions(JustifyContent justify,
			int available, int[] sizes, int gap, int count)
		{
			var positions = new int[count];

			var totalSizes = 0;
			for (var i = 0; i < count; i++)
				totalSizes += sizes[i];

			var totalGaps = count > 1 ? (count - 1) * gap : 0;
			var remaining = available - totalSizes - totalGaps;

			switch (justify)
			{
				case JustifyContent.Start:
				{
					var pos = 0;
					for (var i = 0; i < count; i++)
					{
						positions[i] = pos;
						pos += sizes[i] + gap;
					}

					break;
				}

				case JustifyContent.End:
				{
					var pos = remaining;
					for (var i = 0; i < count; i++)
					{
						positions[i] = pos;
						pos += sizes[i] + gap;
					}

					break;
				}

				case JustifyContent.Center:
				{
					var pos = remaining / 2;
					for (var i = 0; i < count; i++)
					{
						positions[i] = pos;
						pos += sizes[i] + gap;
					}

					break;
				}

				case JustifyContent.SpaceBetween:
				{
					var space = count > 1
						? (float)(available - totalSizes) / (count - 1)
						: 0;
					var pos = 0f;
					for (var i = 0; i < count; i++)
					{
						positions[i] = (int)pos;
						pos += sizes[i] + space;
					}

					break;
				}

				case JustifyContent.SpaceAround:
				{
					var space = count > 0 ? (float)(available - totalSizes) / count : 0;
					var pos = space / 2;
					for (var i = 0; i < count; i++)
					{
						positions[i] = (int)pos;
						pos += sizes[i] + space;
					}

					break;
				}

				case JustifyContent.SpaceEvenly:
				{
					var space = count > 0 ? (float)(available - totalSizes) / (count + 1) : 0;
					var pos = space;
					for (var i = 0; i < count; i++)
					{
						positions[i] = (int)pos;
						pos += sizes[i] + space;
					}

					break;
				}
			}

			return positions;
		}

		static void ApplyCrossAlignment(Widget child, AlignItems parentAlign,
			int availableCross, bool isRow)
		{
			var align = child.AlignSelf != AlignSelf.Auto
				? (AlignItems)((int)child.AlignSelf - 1)
				: parentAlign;

			var childCross = isRow ? child.Bounds.Height : child.Bounds.Width;

			int crossPos;
			switch (align)
			{
				case AlignItems.End:
					crossPos = availableCross - childCross;
					break;
				case AlignItems.Center:
					crossPos = (availableCross - childCross) / 2;
					break;
				case AlignItems.Stretch:
					crossPos = 0;
					childCross = ClampCrossSize(child, availableCross, isRow);
					break;
				default:
					crossPos = 0;
					break;
			}

			if (isRow)
			{
				child.Bounds.Y = crossPos;
				child.Bounds.Height = childCross;
			}
			else
			{
				child.Bounds.X = crossPos;
				child.Bounds.Width = childCross;
			}
		}

		static int ClampMainSize(Widget child, int size, bool isRow)
		{
			return isRow
				? Math.Clamp(size, child.MinWidth, child.MaxWidth)
				: Math.Clamp(size, child.MinHeight, child.MaxHeight);
		}

		static int ClampCrossSize(Widget child, int size, bool isRow)
		{
			return isRow
				? Math.Clamp(size, child.MinHeight, child.MaxHeight)
				: Math.Clamp(size, child.MinWidth, child.MaxWidth);
		}
	}
}
