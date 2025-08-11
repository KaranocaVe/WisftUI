#nullable enable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace WisftUI.Controls
{
    /// <summary>
    /// 一个可以呈现为超椭圆形（Squircle）的边框控件，已针对性能进行优化。
    /// </summary>
    public class SquircleBorder : Decorator
    {

        #region Caching Fields

        // 用于缓存几何图形的私有字段
        private Geometry? _fillGeometryCache;
        private Geometry? _borderGeometryCache;
        private Geometry? _clipGeometryCache;

        // 用于缓存画笔的私有字段
        private Pen? _penCache;

        // 用于判断是否需要重新计算缓存的参数字段
        private Size _cachedBoundsSize;
        private CornerRadius _cachedCornerRadius;
        private double _cachedBorderThickness;
        private double _cachedSmoothness;
        private IBrush? _cachedBorderBrush;

        #endregion

        #region Avalonia Properties

        /// <summary>
        /// 定义 <see cref="CornerRadius"/> 属性，用于设置四个角的圆角半径。
        /// </summary>
        public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
            AvaloniaProperty.Register<SquircleBorder, CornerRadius>(nameof(CornerRadius));

        /// <summary>
        /// 定义 <see cref="Background"/> 属性，用于设置控件的背景画刷。
        /// </summary>
        public static readonly StyledProperty<IBrush?> BackgroundProperty =
            AvaloniaProperty.Register<SquircleBorder, IBrush?>(nameof(Background));

        /// <summary>
        /// 定义 <see cref="BorderBrush"/> 属性，用于设置边框的画刷。
        /// </summary>
        public static readonly StyledProperty<IBrush?> BorderBrushProperty =
            AvaloniaProperty.Register<SquircleBorder, IBrush?>(nameof(BorderBrush));

        /// <summary>
        /// 定义 <see cref="BorderThickness"/> 属性，用于设置边框的粗细。
        /// </summary>
        public static readonly StyledProperty<double> BorderThicknessProperty =
            AvaloniaProperty.Register<SquircleBorder, double>(nameof(BorderThickness));

        /// <summary>
        /// 定义 <see cref="Smoothness"/> 属性，控制超椭圆曲线的平滑程度。
        /// 值从 0.0 (近似矩形) 到 1.0 (标准圆角) 及以上 (趋向圆形)。
        /// </summary>
        public static readonly StyledProperty<double> SmoothnessProperty =
            AvaloniaProperty.Register<SquircleBorder, double>(nameof(Smoothness), 1.0);

        #endregion

        #region CLR Property Wrappers

        /// <summary>
        /// 获取或设置四个角的圆角半径。
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>
        /// 获取或设置控件的背景画刷。
        /// </summary>
        public IBrush? Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        /// <summary>
        /// 获取或设置边框的画刷。
        /// </summary>
        public IBrush? BorderBrush
        {
            get => GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        /// <summary>
        /// 获取或设置边框的粗细。
        /// </summary>
        public double BorderThickness
        {
            get => GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        /// <summary>
        /// 获取或设置超椭圆曲线的平滑程度。
        /// </summary>
        public double Smoothness
        {
            get => GetValue(SmoothnessProperty);
            set => SetValue(SmoothnessProperty, value);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 静态构造函数，用于注册属性元数据和依赖关系。
        /// </summary>
        static SquircleBorder()
        {
            // Padding 和 BorderThickness 的变化会影响控件的测量。
            AffectsMeasure<SquircleBorder>(PaddingProperty, BorderThicknessProperty);

            // 以下属性的变化会影响控件的渲染。
            AffectsRender<SquircleBorder>(
                CornerRadiusProperty,
                BackgroundProperty,
                BorderBrushProperty,
                BorderThicknessProperty,
                SmoothnessProperty
            );
        }

        #endregion

        #region Layout Overrides

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            Control? child = Child;
            Thickness padding = Padding;
            Thickness borderThickness = new Thickness(BorderThickness);
            Thickness borderAndPadding = padding + borderThickness;

            if (child != null)
            {
                Size childConstraint = availableSize.Deflate(borderAndPadding);
                child.Measure(childConstraint);
                return child.DesiredSize.Inflate(borderAndPadding);
            }

            return new Size(borderAndPadding.Left + borderAndPadding.Right,
                borderAndPadding.Top + borderAndPadding.Bottom);
        }

        /// <inheritdoc/>
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

        #endregion

        #region Render Override

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            Rect bounds = Bounds;
            double borderThickness = BorderThickness;
            CornerRadius radii = CornerRadius;
            double smoothness = Smoothness;
            IBrush? borderBrush = BorderBrush;
            IBrush? background = Background;

            // 检查缓存是否有效
            bool useCache =
                _fillGeometryCache != null &&
                _borderGeometryCache != null &&
                _clipGeometryCache != null &&
                _cachedBoundsSize == bounds.Size &&
                _cachedCornerRadius == radii &&
                Math.Abs(_cachedBorderThickness - borderThickness) < 0.01 &&
                Math.Abs(_cachedSmoothness - smoothness) < 0.01;

            Geometry fillGeometry, borderGeometry, clipGeometry;

            if (useCache)
            {
                // 直接使用缓存的几何图形
                fillGeometry = _fillGeometryCache!;
                borderGeometry = _borderGeometryCache!;
                clipGeometry = _clipGeometryCache!;
            }
            else
            {
                // 重新计算并更新缓存
                const double smoothRatio = 10.0;
                double smooth = smoothness * smoothRatio;
                if (smooth <= 0) smooth = 1;

                double minDimensionHalf = Math.Min(bounds.Width, bounds.Height) / 2.0;
                CornerRadius adjustedRadii = new CornerRadius(
                    Math.Min(radii.TopLeft, minDimensionHalf),
                    Math.Min(radii.TopRight, minDimensionHalf),
                    Math.Min(radii.BottomRight, minDimensionHalf),
                    Math.Min(radii.BottomLeft, minDimensionHalf)
                );

                if (borderBrush != null && borderThickness > 0)
                {
                    fillGeometry = CreateSquircleGeometry(bounds.Size, adjustedRadii, smooth, borderThickness);
                    borderGeometry = CreateSquircleGeometry(bounds.Size, adjustedRadii, smooth, borderThickness / 2.0);
                }
                else
                {
                    fillGeometry = CreateSquircleGeometry(bounds.Size, adjustedRadii, smooth, 0);
                    borderGeometry = fillGeometry; // 如果没边框，用一个空占位
                }
                clipGeometry = CreateSquircleGeometry(bounds.Size, adjustedRadii, smooth, 0);

                // 更新缓存
                _fillGeometryCache = fillGeometry;
                _borderGeometryCache = borderGeometry;
                _clipGeometryCache = clipGeometry;
                _cachedBoundsSize = bounds.Size;
                _cachedCornerRadius = radii;
                _cachedBorderThickness = borderThickness;
                _cachedSmoothness = smoothness;
            }

            // 缓存 Pen 物件
            Pen? borderPen = null;
            if (borderBrush != null && borderThickness > 0)
            {
                if (_penCache == null || !Equals(_cachedBorderBrush, borderBrush) || Math.Abs(_cachedBorderThickness - borderThickness) > 0.01)
                {
                    _penCache = new Pen(borderBrush, borderThickness);
                    _cachedBorderBrush = borderBrush;
                }
                borderPen = _penCache;
            }

            // 绘制逻辑 (现在使用缓存或新计算出的物件)
            if (background != null)
            {
                context.DrawGeometry(background, null, fillGeometry);
            }

            if (borderPen != null)
            {
                context.DrawGeometry(null, borderPen, borderGeometry);
            }

            // 裁剪子元素
            using (context.PushGeometryClip(clipGeometry))
            {
                base.Render(context);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 根据给定的参数，创建一个超椭圆几何图形。
        /// </summary>
        private static Geometry CreateSquircleGeometry(Size size, CornerRadius radii, double smoothness, double inset)
        {
            // 根据内缩距离，计算调整后的新圆角半径，防止“收缩过度”
            CornerRadius adjustedRadii = new CornerRadius(
                Math.Max(0, radii.TopLeft - inset),
                Math.Max(0, radii.TopRight - inset),
                Math.Max(0, radii.BottomRight - inset),
                Math.Max(0, radii.BottomLeft - inset)
            );

            StreamGeometry geometry = new StreamGeometry();
            using StreamGeometryContext context = geometry.Open();
            context.BeginFigure(new Point(adjustedRadii.TopLeft + inset, inset), true);

            // Top line and top-right corner
            context.LineTo(new Point(size.Width - adjustedRadii.TopRight - inset, inset));
            context.CubicBezierTo(
                new Point(size.Width - adjustedRadii.TopRight / smoothness - inset, inset),
                new Point(size.Width - inset, adjustedRadii.TopRight / smoothness + inset),
                new Point(size.Width - inset, adjustedRadii.TopRight + inset));

            // Right line and bottom-right corner
            context.LineTo(new Point(size.Width - inset, size.Height - adjustedRadii.BottomRight - inset));
            context.CubicBezierTo(
                new Point(size.Width - inset, size.Height - adjustedRadii.BottomRight / smoothness - inset),
                new Point(size.Width - adjustedRadii.BottomRight / smoothness - inset, size.Height - inset),
                new Point(size.Width - adjustedRadii.BottomRight - inset, size.Height - inset));

            // Bottom line and bottom-left corner
            context.LineTo(new Point(adjustedRadii.BottomLeft + inset, size.Height - inset));
            context.CubicBezierTo(
                new Point(adjustedRadii.BottomLeft / smoothness + inset, size.Height - inset),
                new Point(inset, size.Height - adjustedRadii.BottomLeft / smoothness - inset),
                new Point(inset, size.Height - adjustedRadii.BottomLeft - inset));

            // Left line and top-left corner
            context.LineTo(new Point(inset, adjustedRadii.TopLeft + inset));
            context.CubicBezierTo(
                new Point(inset, adjustedRadii.TopLeft / smoothness + inset),
                new Point(adjustedRadii.TopLeft / smoothness + inset, inset),
                new Point(adjustedRadii.TopLeft + inset, inset));

            context.EndFigure(true);
            return geometry;
        }

        #endregion

    }
}
