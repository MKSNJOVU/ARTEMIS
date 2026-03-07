using Avalonia;
using Avalonia.Controls;
using System;

namespace Artemis.Desktop.Services;

/// <summary>
/// Provides attached properties for secure password handling in Avalonia.
/// This minimizes the lifespan of password strings by converting them to char arrays immediately.
/// </summary>
public static class SecurityHelper
{
    public static readonly AttachedProperty<char[]?> SecurePasswordProperty =
        AvaloniaProperty.RegisterAttached<TextBox, char[]?>("SecurePassword", typeof(SecurityHelper));

    static SecurityHelper()
    {
        // Listen for when the Text property of any TextBox changes
        TextBox.TextProperty.Changed.AddClassHandler<TextBox>((sender, e) =>
        {
            if (sender is TextBox textBox)
            {
                var text = textBox.Text;
                // Convert string to char[] and update the bound property
                SetSecurePassword(textBox, string.IsNullOrEmpty(text) ? null : text.ToCharArray());
            }
        });
    }

    public static char[]? GetSecurePassword(TextBox element) => element.GetValue(SecurePasswordProperty);
    public static void SetSecurePassword(TextBox element, char[]? value) => element.SetValue(SecurePasswordProperty, value);
}
