using System.Globalization;
using System.Diagnostics;
using System.Reflection;

using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows;

using SharpVectors.Converters;

using ModernWpf.Controls;
using ModernWpf;


namespace TabbedExplorer.WPF;


// <Image Source="{Binding Converter={StaticResource ...}, ConverterParameter=shell32.dll|72}"/>
[ValueConversion(typeof(string), typeof(ImageSource))]
public sealed class NativeIconToImageSource
    : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string[] fileName = ((string)parameter).Split('|');

        if (targetType != typeof(ImageSource))
            return Binding.DoNothing;

        nint hproc = Process.GetCurrentProcess().Handle;
        nint hIcon = Interop.ExtractIcon(hproc, fileName[0], int.Parse(fileName[1]));

        return Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

// {Binding Converter={StaticResource ...}, ConverterParameter=shell32.dll|72}
[ValueConversion(typeof(string), typeof(Image))]
public sealed class NativeIconToImage
    : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new NativeIconToImageSource().Convert(value, targetType, parameter, culture) switch
        {
            ImageSource source => new Image() { Source = source },
            _ => Binding.DoNothing
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

[ValueConversion(typeof(double), typeof(GridLength))]
public sealed class PixelsToGridLengthConverter
    : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => new GridLength((double)value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => ((GridLength)value).Value;
}

[ValueConversion(typeof(Visibility), typeof(Visibility))]
public sealed class VisibilityInverter
    : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        UIElement { Visibility: Visibility v } => Convert(v, targetType, parameter, culture),
        Visibility.Collapsed or Visibility.Hidden => Visibility.Visible,
        Visibility.Visible => Visibility.Hidden,
        _ => throw new NotImplementedException(),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Convert(value, targetType, parameter, culture);
}

[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullVisibilityConverter
    : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(bool))]
public sealed class BooleanInverter
    : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Convert(value, targetType, parameter, culture);
}

public sealed class LeftClickContextMenu
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(FrameworkElement), new PropertyMetadata(false, EnabledPropertyChanged));


    public static void SetEnabled(FrameworkElement element, bool value) => element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(FrameworkElement element) => (bool)element.GetValue(EnabledProperty);

    private static void EnabledPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj.GetType().GetEvent("Click") is EventInfo evt)
        {
            evt.RemoveEventHandler(obj, new RoutedEventHandler(ExecMouseDown));
            evt.AddEventHandler(obj, new RoutedEventHandler(ExecMouseDown));
        }
        else if (obj is FrameworkElement elem)
        {
            elem.MouseLeftButtonDown -= ExecMouseDown;
            elem.MouseLeftButtonDown += ExecMouseDown;
        }
    }

    private static void ExecMouseDown(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { ContextMenu: not null } elem && GetEnabled(elem))
            elem.ContextMenu.IsOpen = true;
    }
}

public sealed class ImageIcon
    : BitmapIcon
{
    public static readonly DependencyProperty ImageProperty =
        DependencyProperty.Register(nameof(Image), typeof(Image), typeof(ImageIcon), new PropertyMetadata(null, (d, e) =>
        {
            ((ImageIcon)d).ApplyImage();
        }));


    public Image? Image
    {
        get => GetValue(ShowAsMonochromeProperty) as Image;
        set => SetValue(ShowAsMonochromeProperty, value);
    }

    private static FieldInfo _image = typeof(BitmapIcon).GetField(nameof(_image), BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidProgramException();


    public ImageIcon()
    {
    }

    private void ApplyImage()
    {
        Image? image = Image ?? new Image { Visibility = Visibility.Hidden };

        _image.SetValue(this, image);
    }
}

public sealed class ThemedSVGButton
    : Button
{
    public static readonly DependencyProperty ImageNameProperty =
        DependencyProperty.Register(nameof(ImageName), typeof(string), typeof(ThemedSVGButton), new PropertyMetadata(null, (s, e) =>
        {
            if (s is ThemedSVGButton button)
                button.UpdateImageSource(e.NewValue as string);
        }));

    public string? ImageName
    {
        get => GetValue(ImageNameProperty) as string;
        set => SetValue(ImageNameProperty, value);
    }

    public ThemeManager Manager { get; }


    public ThemedSVGButton()
    {
        Height =
        Width = 30;
        Background = Brushes.Transparent;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Padding = new(5);
        Margin = new(2);
        Content = new Image();
        Manager = ThemeManager.Current;

        ThemeManager.AddActualThemeChangedHandler(this, OnThemeChanged);
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e) => UpdateImageSource(ImageName);

    private void UpdateImageSource(string? name)
    {
        if (Content is not Image image)
            image = new Image();

        if (name is null)
            image.Source = null;
        else
        {
            string uri = $"/images/theme-{(Manager.ActualApplicationTheme is ApplicationTheme.Dark ? "dark" : "light")}/{name}.svg";
            SvgImageExtension ext = new(uri);
            URIContextServiceProvider provider = new(image);

            if (ext.ProvideValue(provider) is ImageSource source)
                image.Source = source;
        }

        Content = image;
    }

    private sealed record URIContextServiceProvider(Image Image)
        : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IUriContext) ? Image : (object?)null;
    }
}

public static class WPFExtensions
{
    public static T? GetChildOfType<T>(this DependencyObject? obj)
        where T : DependencyObject
    {
        if (obj is { })
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (((child as T) ?? GetChildOfType<T>(child)) is T result)
                    return result;
            }

        return null;
    }
}

