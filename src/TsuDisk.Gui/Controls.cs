using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Gui;

/// <summary>Xep con theo chieu ngang, rong ti le voi <see cref="FractionProperty"/> (tong = 1) - thanh ban do dia.</summary>
internal sealed class FractionPanel : Panel
{
    public static readonly DependencyProperty FractionProperty = DependencyProperty.RegisterAttached(
        "Fraction", typeof(double), typeof(FractionPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsParentArrange | FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public static double GetFraction(DependencyObject element) => (double)element.GetValue(FractionProperty);

    public static void SetFraction(DependencyObject element, double value) => element.SetValue(FractionProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        var height = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(width * GetFraction(child), availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            var w = finalSize.Width * GetFraction(child);
            child.Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w;
        }
        return finalSize;
    }
}

/// <summary>Vai tro khoi -> ten tai nguyen mau token (nen, chu).</summary>
internal sealed class RoleBrushConverter : IValueConverter
{
    public bool Foreground { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = (value is MapRole role ? role : MapRole.Other, Foreground) switch
        {
            (MapRole.Data, false) => "primary",
            (MapRole.Data, true) => "on-primary",
            (MapRole.System, false) => "info",
            (MapRole.Recovery, false) => "warning",
            (MapRole.System or MapRole.Recovery, true) => "on-status",
            (MapRole.Free, false) => "bg-subtle",
            (MapRole.Free, true) => "text-secondary",
            (_, false) => "text-muted",
            (_, true) => "bg-base",
        };
        return Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>true/khac rong -> Visible; <see cref="Invert"/> dao lai.</summary>
internal sealed class VisibleConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var on = value switch
        {
            bool b => b,
            string s => s.Length > 0,
            System.Collections.ICollection c => c.Count > 0,
            null => false,
            _ => true,
        };
        return on ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
