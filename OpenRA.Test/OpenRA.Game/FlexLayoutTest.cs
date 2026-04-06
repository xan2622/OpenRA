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

using NUnit.Framework;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class FlexLayoutTest
	{
		static ContainerWidget MakeContainer(int width, int height)
		{
			return new ContainerWidget() { Bounds = new WidgetBounds(0, 0, width, height) };
		}

		static ContainerWidget MakeFlexChild(int width, int height)
		{
			return new ContainerWidget()
			{
				Bounds = new WidgetBounds(0, 0, width, height),
				Positioning = WidgetLayout.Flex
			};
		}

		[Test]
		public void ColumnLayout_StacksVertically()
		{
			var parent = MakeContainer(200, 400);
			parent.FlexDirection = FlexDirection.Column;

			var a = MakeFlexChild(200, 50);
			var b = MakeFlexChild(200, 80);
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Y, Is.EqualTo(0));
			Assert.That(b.Bounds.Y, Is.EqualTo(50));
		}

		[Test]
		public void RowLayout_StacksHorizontally()
		{
			var parent = MakeContainer(400, 100);
			parent.FlexDirection = FlexDirection.Row;

			var a = MakeFlexChild(100, 100);
			var b = MakeFlexChild(150, 100);
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.X, Is.EqualTo(0));
			Assert.That(b.Bounds.X, Is.EqualTo(100));
		}

		[Test]
		public void Gap_AddsSpaceBetweenItems()
		{
			var parent = MakeContainer(400, 100);
			parent.FlexDirection = FlexDirection.Row;
			parent.Gap = 20;

			var a = MakeFlexChild(100, 100);
			var b = MakeFlexChild(100, 100);
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.X, Is.EqualTo(0));
			Assert.That(b.Bounds.X, Is.EqualTo(120)); // 100 + 20 gap
		}

		[Test]
		public void FlexGrow_DistributesFreeSpace()
		{
			var parent = MakeContainer(300, 100);
			parent.FlexDirection = FlexDirection.Row;

			var a = MakeFlexChild(50, 100);
			a.FlexGrow = 1;
			var b = MakeFlexChild(50, 100);
			b.FlexGrow = 2;
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			// 200px free space, split 1:2 => a gets ~67, b gets ~133
			// a: 50 + 66 = 116, b: 50 + 133 = 183 (rounding: a gets +1)
			Assert.That(a.Bounds.Width + b.Bounds.Width, Is.EqualTo(300));
			Assert.That(a.Bounds.Width, Is.GreaterThan(50));
			Assert.That(b.Bounds.Width, Is.GreaterThan(a.Bounds.Width));
		}

		[Test]
		public void FlexShrink_ShrinkOverflowingItems()
		{
			var parent = MakeContainer(200, 100);
			parent.FlexDirection = FlexDirection.Row;

			var a = MakeFlexChild(150, 100);
			a.FlexShrink = 1;
			var b = MakeFlexChild(150, 100);
			b.FlexShrink = 1;
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Width, Is.EqualTo(100));
			Assert.That(b.Bounds.Width, Is.EqualTo(100));
		}

		[Test]
		public void JustifyCenter_CentersItems()
		{
			var parent = MakeContainer(400, 100);
			parent.FlexDirection = FlexDirection.Row;
			parent.JustifyContent = JustifyContent.Center;

			var a = MakeFlexChild(100, 100);
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.X, Is.EqualTo(150)); // (400-100)/2
		}

		[Test]
		public void JustifyEnd_AlignsToEnd()
		{
			var parent = MakeContainer(400, 100);
			parent.FlexDirection = FlexDirection.Row;
			parent.JustifyContent = JustifyContent.End;

			var a = MakeFlexChild(100, 100);
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.X, Is.EqualTo(300)); // 400-100
		}

		[Test]
		public void JustifySpaceBetween_SpacesItems()
		{
			var parent = MakeContainer(400, 100);
			parent.FlexDirection = FlexDirection.Row;
			parent.JustifyContent = JustifyContent.SpaceBetween;

			var a = MakeFlexChild(50, 100);
			var b = MakeFlexChild(50, 100);
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.X, Is.EqualTo(0));
			Assert.That(b.Bounds.X, Is.EqualTo(350)); // 400 - 50
		}

		[Test]
		public void AlignItemsCenter_CentersOnCrossAxis()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;
			parent.AlignItems = AlignItems.Center;

			var a = MakeFlexChild(100, 50);
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Y, Is.EqualTo(75)); // (200-50)/2
		}

		[Test]
		public void AlignItemsStretch_StretchesToCrossAxis()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;
			parent.AlignItems = AlignItems.Stretch;

			var a = MakeFlexChild(100, 50);
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Height, Is.EqualTo(200));
		}

		[Test]
		public void AlignSelf_OverridesAlignItems()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;
			parent.AlignItems = AlignItems.Start;

			var a = MakeFlexChild(100, 50);
			a.AlignSelf = AlignSelf.End;
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Y, Is.EqualTo(150)); // 200 - 50
		}

		[Test]
		public void Padding_ReducesAvailableSpace()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;
			parent.Padding = new EdgeInsets(10, 20, 10, 20);

			var a = MakeFlexChild(100, 50);
			a.FlexGrow = 1;
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			// Available width: 400 - 20 - 20 = 360
			Assert.That(a.Bounds.Width, Is.EqualTo(360));
		}

		[Test]
		public void Margin_ReducesFreeSpaceForGrow()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;

			var a = MakeFlexChild(100, 50);
			a.FlexGrow = 1;
			a.Margin = new EdgeInsets(0, 0, 0, 20); // 20px left margin
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			// Available main: 400, margin: 20, so item grows to 400-20 = 380
			Assert.That(a.Bounds.Width, Is.EqualTo(380));
			Assert.That(a.Bounds.X, Is.EqualTo(20)); // offset by left margin
		}

		[Test]
		public void Margin_OffsetsCrossAxis()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;
			parent.AlignItems = AlignItems.Start;

			var a = MakeFlexChild(100, 50);
			a.Margin = new EdgeInsets(15, 0, 0, 0); // 15px top margin
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Y, Is.EqualTo(15));
		}

		[Test]
		public void FlexWrap_WrapsToNewLine()
		{
			var parent = MakeContainer(200, 400);
			parent.FlexDirection = FlexDirection.Row;
			parent.FlexWrap = FlexWrap.Wrap;

			var a = MakeFlexChild(120, 50);
			var b = MakeFlexChild(120, 50);
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			// a (120px) fits on first line, b (120px) wraps to second line
			Assert.That(a.Bounds.Y, Is.EqualTo(0));
			Assert.That(b.Bounds.Y, Is.EqualTo(50)); // second line
		}

		[Test]
		public void AbsoluteChildrenIgnoredByFlex()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;

			var abs = new ContainerWidget()
			{
				Bounds = new WidgetBounds(10, 10, 50, 50),
				Positioning = WidgetLayout.Absolute
			};

			var flex = MakeFlexChild(100, 100);
			flex.FlexGrow = 1;

			parent.AddChild(abs);
			parent.AddChild(flex);

			FlexLayout.PerformLayout(parent);

			// Absolute child is untouched
			Assert.That(abs.Bounds.X, Is.EqualTo(10));
			Assert.That(abs.Bounds.Y, Is.EqualTo(10));

			// Flex child takes all space
			Assert.That(flex.Bounds.Width, Is.EqualTo(400));
		}

		[Test]
		public void IntrinsicSize_Column()
		{
			var parent = MakeContainer(0, 0);
			parent.FlexDirection = FlexDirection.Column;
			parent.Gap = 10;

			var a = MakeFlexChild(100, 50);
			var b = MakeFlexChild(120, 80);
			parent.AddChild(a);
			parent.AddChild(b);

			var (w, h) = FlexLayout.CalculateIntrinsicSize(parent);

			Assert.That(w, Is.EqualTo(120)); // max width
			Assert.That(h, Is.EqualTo(140)); // 50 + 10 + 80
		}

		[Test]
		public void IntrinsicSize_Row()
		{
			var parent = MakeContainer(0, 0);
			parent.FlexDirection = FlexDirection.Row;
			parent.Gap = 5;

			var a = MakeFlexChild(60, 30);
			var b = MakeFlexChild(80, 50);
			parent.AddChild(a);
			parent.AddChild(b);

			var (w, h) = FlexLayout.CalculateIntrinsicSize(parent);

			Assert.That(w, Is.EqualTo(145)); // 60 + 5 + 80
			Assert.That(h, Is.EqualTo(50)); // max height
		}

		[Test]
		public void IntrinsicSize_WithPaddingAndMargin()
		{
			var parent = MakeContainer(0, 0);
			parent.FlexDirection = FlexDirection.Row;
			parent.Padding = new EdgeInsets(10);

			var a = MakeFlexChild(100, 50);
			a.Margin = new EdgeInsets(5);
			parent.AddChild(a);

			var (w, h) = FlexLayout.CalculateIntrinsicSize(parent);

			// Width: 100 + 10(margin H) + 20(padding H) = 130
			Assert.That(w, Is.EqualTo(130));

			// Height: 50 + 10(margin V) + 20(padding V) = 80
			Assert.That(h, Is.EqualTo(80));
		}

		[Test]
		public void MaxWidth_ClampsFlexGrow()
		{
			var parent = MakeContainer(400, 100);
			parent.FlexDirection = FlexDirection.Row;

			var a = MakeFlexChild(50, 100);
			a.FlexGrow = 1;
			a.MaxWidth = 200;
			var b = MakeFlexChild(50, 100);
			b.FlexGrow = 1;
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Width, Is.LessThanOrEqualTo(200));
		}

		[Test]
		public void MinWidth_PreventsFlexShrink()
		{
			var parent = MakeContainer(100, 100);
			parent.FlexDirection = FlexDirection.Row;

			var a = MakeFlexChild(80, 100);
			a.FlexShrink = 1;
			a.MinWidth = 60;
			var b = MakeFlexChild(80, 100);
			b.FlexShrink = 1;
			parent.AddChild(a);
			parent.AddChild(b);

			FlexLayout.PerformLayout(parent);

			Assert.That(a.Bounds.Width, Is.GreaterThanOrEqualTo(60));
		}

		[Test]
		public void Border_ReducesAvailableSpace()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;
			parent.Border = new EdgeInsets(5, 10, 5, 10);

			var a = MakeFlexChild(100, 50);
			a.FlexGrow = 1;
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			// Available width: 400 - 20(border H) = 380
			Assert.That(a.Bounds.Width, Is.EqualTo(380));
		}

		[Test]
		public void Border_CombinesWithPadding()
		{
			var parent = MakeContainer(400, 200);
			parent.FlexDirection = FlexDirection.Row;
			parent.Padding = new EdgeInsets(10);
			parent.Border = new EdgeInsets(5);

			var a = MakeFlexChild(100, 50);
			a.FlexGrow = 1;
			parent.AddChild(a);

			FlexLayout.PerformLayout(parent);

			// Available width: 400 - 20(padding H) - 10(border H) = 370
			Assert.That(a.Bounds.Width, Is.EqualTo(370));
		}

		[Test]
		public void IntrinsicSize_IncludesBorder()
		{
			var parent = MakeContainer(0, 0);
			parent.FlexDirection = FlexDirection.Row;
			parent.Border = new EdgeInsets(5);

			var a = MakeFlexChild(100, 50);
			parent.AddChild(a);

			var (w, h) = FlexLayout.CalculateIntrinsicSize(parent);

			// Width: 100 + 10(border H) = 110
			Assert.That(w, Is.EqualTo(110));

			// Height: 50 + 10(border V) = 60
			Assert.That(h, Is.EqualTo(60));
		}
	}
}
