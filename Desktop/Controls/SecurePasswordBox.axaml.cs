using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Artemis.Desktop.Controls;

public partial class SecurePasswordBox : UserControl
{
    public static readonly StyledProperty<byte[]?> SecurePasswordProperty =
        AvaloniaProperty.Register<SecurePasswordBox, byte[]?>(nameof(SecurePassword), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<SecurePasswordBox, string>(nameof(Watermark), "Password");

    public static readonly StyledProperty<char> PasswordMaskProperty =
        AvaloniaProperty.Register<SecurePasswordBox, char>(nameof(PasswordMask), '*');

    public static readonly StyledProperty<string> ToggleTipProperty =
        AvaloniaProperty.Register<SecurePasswordBox, string>(nameof(ToggleTip), "Show password");

    public static readonly StyledProperty<Geometry?> ToggleIconProperty =
        AvaloniaProperty.Register<SecurePasswordBox, Geometry?>(nameof(ToggleIcon));

    private bool _isVisible;

    public SecurePasswordBox()
    {
        InitializeComponent();
        UpdateToggleVisuals();
        Loaded += (_, _) => UpdateToggleVisuals();
        SecurePasswordProperty.Changed.AddClassHandler<SecurePasswordBox>((box, _) => box.OnSecurePasswordChanged());
    }

    public byte[]? SecurePassword
    {
        get => GetValue(SecurePasswordProperty);
        set => SetValue(SecurePasswordProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public char PasswordMask
    {
        get => GetValue(PasswordMaskProperty);
        set => SetValue(PasswordMaskProperty, value);
    }

    public string ToggleTip
    {
        get => GetValue(ToggleTipProperty);
        set => SetValue(ToggleTipProperty, value);
    }

    public Geometry? ToggleIcon
    {
        get => GetValue(ToggleIconProperty);
        set => SetValue(ToggleIconProperty, value);
    }

    private void OnToggleVisibility(object? sender, RoutedEventArgs e)
    {
        _isVisible = !_isVisible;
        PasswordMask = _isVisible ? '\0' : '*';
        UpdateToggleVisuals();
    }

    private void OnSecurePasswordChanged()
    {
        if (SecurePassword is null or { Length: 0 } && !string.IsNullOrEmpty(Input.Text))
            Input.Text = string.Empty;
    }

    private void UpdateToggleVisuals()
    {
        ToggleTip = _isVisible ? "Hide password" : "Show password";
        object? resource = Application.Current?.FindResource(_isVisible ? "IconEyeOff" : "IconEye");
        ToggleIcon = resource as Geometry;
    }
}
