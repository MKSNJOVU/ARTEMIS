using Avalonia;
using Avalonia.Controls;
using System;

namespace Artemis.Desktop.Services;

/// <summary>
/// Provides an attached property that exposes a <see cref="TextBox"/>'s password as a <see cref="char"/> array.
/// This avoids storing the password as a <see cref="string"/> in the view model or other bound data, but the password
/// still resides in <see cref="TextBox.Text"/> as a <see cref="string"/> managed by the UI framework.
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
                // Clear any previously stored password buffer to minimize secret lifetime
                var oldBuffer = GetSecurePassword(textBox);
                if (oldBuffer != null)
                    Array.Clear(oldBuffer, 0, oldBuffer.Length);

                var text = textBox.Text;
                // Convert string to char[] and update the bound property
                var newBuffer = string.IsNullOrEmpty(text) ? null : text.ToCharArray();
                SetSecurePassword(textBox, newBuffer);
            }
        });
    }

    public static char[]? GetSecurePassword(TextBox element) => element.GetValue(SecurePasswordProperty);
    public static void SetSecurePassword(TextBox element, char[]? value) => element.SetValue(SecurePasswordProperty, value);
}
