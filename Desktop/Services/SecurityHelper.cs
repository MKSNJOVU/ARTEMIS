using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Artemis.Desktop.Services;

/// <summary>
/// Provides an attached property that exposes a <see cref="TextBox"/>'s password as a <see cref="byte"/> array.
/// This avoids storing the password as a <see cref="string"/> in the view model or other bound data, but the password
/// still resides in <see cref="TextBox.Text"/> as a <see cref="string"/> managed by the UI framework.
/// </summary>
public class SecurityHelper : AvaloniaObject
{
    /*  static SecurityHelper()
      {
          // EXCELLENCE: Instead of listening to EVERY TextBox globally, 
          // we only react when our property is ATTACHED to a specific instance.
          SecurePasswordProperty.Changed.AddClassHandler<TextBox>(HandleSecurePasswordChanged);
      }

      private static readonly ConditionalWeakTable<TextBox, IDisposable> _textSubscriptions = new();

      public static readonly AttachedProperty<byte[]?> SecurePasswordProperty =
          AvaloniaProperty.RegisterAttached<SecurityHelper, TextBox, byte[]?>("SecurePassword", default(byte[]?), false, BindingMode.TwoWay);


      private static void HandleSecurePasswordChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
      {
          // If the property is being set and we aren't already watching this instance
          if (!_textSubscriptions.TryGetValue(textBox, out _))
          {
              // SURGICAL: Subscribe ONLY to this specific TextBox's Text changes
              var subscription = textBox.GetObservable(TextBox.TextProperty).Subscribe(_ => UpdateBuffer(textBox));

              // LEAK PROTECTION: Store the subscription in a table that lets go when the TextBox is destroyed
              _textSubscriptions.Add(textBox, subscription);
          }


      }

      private static void UpdateBuffer(TextBox textBox)
      {
          // Clear any previously stored password buffer to minimize secret lifetime
          var oldBuffer = GetSecurePassword(textBox);
          if (oldBuffer is not null)
              Array.Clear(oldBuffer, 0, oldBuffer.Length);

          var text = textBox.Text;
          // Convert string to byte[] and update the bound property
          var newBuffer = string.IsNullOrEmpty(text) ? null : Encoding.UTF8.GetBytes(text);
          SetSecurePassword(textBox, newBuffer);
      }

      public static byte[]? GetSecurePassword(AvaloniaObject element) => element.GetValue(SecurePasswordProperty);
      public static void SetSecurePassword(AvaloniaObject element, byte[]? value) => element.SetValue(SecurePasswordProperty, value);
    */


    private static readonly ConditionalWeakTable<TextBox, IDisposable> _textSubscriptions = new();

    public static readonly AttachedProperty<byte[]?> SecurePasswordProperty =
        AvaloniaProperty.RegisterAttached<SecurityHelper, TextBox, byte[]?>("SecurePassword", default(byte[]?), false, BindingMode.TwoWay);

    // 1. THE GRID (Runs ONCE when the app starts)
    static SecurityHelper()
    {
        Debug.WriteLine("✅ STEP 1: Static Constructor fired. Wiring up the Avalonia property.");
        SecurePasswordProperty.Changed.AddClassHandler<TextBox>(HandleSecurePasswordChanged);
    }

    // 2. THE PLUG (Runs TWICE when the UI loads - once for each TextBox)
    private static void HandleSecurePasswordChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_textSubscriptions.TryGetValue(textBox, out _))
        {
            Debug.WriteLine("✅ STEP 2: Attached Property found a TextBox. Plugging in the radar.");

            var subscription = textBox.GetObservable(TextBox.TextProperty)
                                      .Skip(1)
                                      .Subscribe(_ =>
            {
                // 3. THE LIGHT SWITCH (Runs EVERY TIME you press a key)
                Debug.WriteLine($"✅ STEP 3: Keystroke detected! Current text length: {textBox.Text?.Length ?? 0}");
                UpdateBuffer(textBox);
            });

            _textSubscriptions.Add(textBox, subscription);
        }
    }

    private static void UpdateBuffer(TextBox textBox)
    {
        var oldBuffer = GetSecurePassword(textBox);
        if (oldBuffer is not null)
            Array.Clear(oldBuffer, 0, oldBuffer.Length);

        var text = textBox.Text;
        var newBuffer = string.IsNullOrEmpty(text) ? null : Encoding.UTF8.GetBytes(text);

        SetSecurePassword(textBox, newBuffer);
        Debug.WriteLine("✅ STEP 4: Buffer updated and pushed to ViewModel!");
    }

    public static byte[]? GetSecurePassword(AvaloniaObject element) => element.GetValue(SecurePasswordProperty);
    public static void SetSecurePassword(AvaloniaObject element, byte[]? value) => element.SetValue(SecurePasswordProperty, value);

    /// <summary>
    /// Securely clears the system clipboard to prevent sensitive data from persisting.
    /// </summary>
    /// <param name="topLevel">The current TopLevel (Window/Control) context.</param>
    public static async System.Threading.Tasks.Task ClearClipboardAsync(TopLevel? topLevel)
    {
        var clipboard = topLevel?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(null);
        }
    }
}
