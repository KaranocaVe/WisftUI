#nullable enable
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace WisftUI.Controls
{
    /// <summary>
    /// 一个可以呈现为超椭圆形的边框控件。
    /// </summary>
    public class SquircleBorder : Decorator
    {
        // --- 自定义属性 ---
        public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
            AvaloniaProperty.Register<SquircleBorder, CornerRadius>(nameof(CornerRadius));

        // Decorator 没有 Background，所以我们自己定义
        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            AvaloniaProperty.Register<SquircleBorder, IBrush?>(nameof(Background));

        public static readonly StyledProperty<IBrush?> BorderBrushProperty =
            AvaloniaProperty.Register<SquircleBorder, IBrush?>(nameof(BorderBrush));

        public static readonly StyledProperty<double> BorderThicknessProperty =
            AvaloniaProperty.Register<SquircleBorder, double>(nameof(BorderThickness), 1.0);

        public static readonly StyledProperty<double> SmoothnessProperty =
            AvaloniaProperty.Register<SquircleBorder, double>(nameof(Smoothness), 1.0);

        // --- 属性包装器 ---
        public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
        public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
        public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
        public double BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
        public double Smoothness { get => GetValue(SmoothnessProperty); set => SetValue(SmoothnessProperty, value); }

        // --- 静态构造函数 ---
        static SquircleBorder()
        {
            // Padding 是从 Decorator 继承的，我们需要告诉系统它的变化会影响测量
            AffectsMeasure<SquircleBorder>(PaddingProperty, BorderThicknessProperty);
            AffectsRender<SquircleBorder>(
                CornerRadiusProperty,
                BackgroundProperty,
                BorderBrushProperty,
                BorderThicknessProperty,
                SmoothnessProperty
            );
        }

        // --- 布局方法 ---
        protected override Size MeasureOverride(Size availableSize)
        {
            Control? child = Child;
            Thickness padding = Padding;
            Thickness borderThickness = new Thickness(BorderThickness);

            if (child != null)
            {
                Size childConstraint = availableSize.Deflate(padding + borderThickness);
                child.Measure(childConstraint);
                return child.DesiredSize.Inflate(padding + borderThickness);
            }

            return new Size(borderThickness.Left + borderThickness.Right + padding.Left + padding.Right,
                borderThickness.Top + borderThickness.Bottom + padding.Top + padding.Bottom);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Control? child = Child;
            if (child != null)
            {
                Thickness padding = Padding;
                Thickness borderThickness = new Thickness(BorderThickness);
                Rect innerRect = new Rect(finalSize).Deflate(padding + borderThickness);
                child.Arrange(innerRect);
            }
            return finalSize;
        }

        /// <summary>
        /// 根据给定的尺寸、圆角、平滑度和内缩距离，创建一个超椭圆几何图形。
        /// <summary>
        /// 根据给定的尺寸、圆角、平滑度和内缩距离，创建一个超椭圆几何图形。
        /// </summary>
        private Geometry CreateSquircleGeometry(Size size, CornerRadius radii, double smoothness, double inset)
        {
            // --- 【内核修正】 ---
            // 根据内缩距离，计算调整后的新圆角半径。
            // 新半径不能小于0。
            CornerRadius adjustedRadii = new CornerRadius(
                Math.Max(0, radii.TopLeft - inset),
                Math.Max(0, radii.TopRight - inset),
                Math.Max(0, radii.BottomRight - inset),
                Math.Max(0, radii.BottomLeft - inset)
            );

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext streamGeometryContext = geometry.Open())
            {
                // 所有绘制指令现在都使用调整后的 adjustedRadii
                streamGeometryContext.BeginFigure(new Point(adjustedRadii.TopLeft + inset, inset), true);

                streamGeometryContext.LineTo(new Point(size.Width - adjustedRadii.TopRight - inset, inset));
                streamGeometryContext.CubicBezierTo(
                    new Point(size.Width - adjustedRadii.TopRight / smoothness - inset, inset),
                    new Point(size.Width - inset, adjustedRadii.TopRight / smoothness + inset),
                    new Point(size.Width - inset, adjustedRadii.TopRight + inset));

                streamGeometryContext.LineTo(new Point(size.Width - inset, size.Height - adjustedRadii.BottomRight - inset));
                streamGeometryContext.CubicBezierTo(
                    new Point(size.Width - inset, size.Height - adjustedRadii.BottomRight / smoothness - inset),
                    new Point(size.Width - adjustedRadii.BottomRight / smoothness - inset, size.Height - inset),
                    new Point(size.Width - adjustedRadii.BottomRight - inset, size.Height - inset));

                streamGeometryContext.LineTo(new Point(adjustedRadii.BottomLeft + inset, size.Height - inset));
                streamGeometryContext.CubicBezierTo(
                    new Point(adjustedRadii.BottomLeft / smoothness + inset, size.Height - inset),
                    new Point(inset, size.Height - adjustedRadii.BottomLeft / smoothness - inset),
                    new Point(inset, size.Height - adjustedRadii.BottomLeft - inset));

                streamGeometryContext.LineTo(new Point(inset, adjustedRadii.TopLeft + inset));
                streamGeometryContext.CubicBezierTo(
                    new Point(inset, adjustedRadii.TopLeft / smoothness + inset),
                    new Point(adjustedRadii.TopLeft / smoothness + inset, inset),
                    new Point(adjustedRadii.TopLeft + inset, inset));

                streamGeometryContext.EndFigure(true);
            }
            return geometry;
        }
        public override void Render(DrawingContext context)
        {
            // 1. 基本参数计算 (不变)
            const double smoothRatio = 10.0;
            double smooth = Smoothness * smoothRatio;
            if (smooth <= 0) smooth = 1;

            Rect bounds = Bounds;
            double borderThickness = BorderThickness;
            CornerRadius radii = CornerRadius;

            double minDimensionHalf = Math.Min(bounds.Width, bounds.Height) / 2.0;
            radii = new CornerRadius(
                Math.Min(radii.TopLeft, minDimensionHalf),
                Math.Min(radii.TopRight, minDimensionHalf),
                Math.Min(radii.BottomRight, minDimensionHalf),
                Math.Min(radii.BottomLeft, minDimensionHalf)
            );


            // 2. 绘制逻辑
            IBrush? background = Background;
            IBrush? borderBrush = BorderBrush;

            // A. 如果有边框
            if (borderBrush != null && borderThickness > 0)
            {
                // 为背景创建一个精确位于边框【内侧】的几何图形
                // 内缩距离 = 整个边框厚度
                Geometry fillGeometry = CreateSquircleGeometry(bounds.Size, radii, smooth, borderThickness);

                // 为边框创建一个位于【中心线】的几何图形
                // 内缩距离 = 边框厚度的一半
                Geometry borderGeometry = CreateSquircleGeometry(bounds.Size, radii, smooth, borderThickness / 2.0);

                // 先填充背景，它绝不会越过边框的内边缘
                if (background != null)
                {
                    context.DrawGeometry(background, null, fillGeometry);
                }

                // 再绘制边框，它的内半部分会与背景的边缘完美贴合
                Pen borderPen = new Pen(borderBrush, borderThickness);
                context.DrawGeometry(null, borderPen, borderGeometry);
            }
            // B. 如果没有边框
            else if (background != null)
            {
                // 背景直接填充整个控件区域
                Geometry fillGeometry = CreateSquircleGeometry(bounds.Size, radii, smooth, 0);
                context.DrawGeometry(background, null, fillGeometry);
            }

            // 3. 裁剪子元素 (逻辑不变)
            Geometry outerClipGeometry = CreateSquircleGeometry(bounds.Size, radii, smooth, 0);
            using (context.PushGeometryClip(outerClipGeometry))
            {
                base.Render(context);
            }
        }
    }
}
