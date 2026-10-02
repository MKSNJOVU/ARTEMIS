using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Artemis.Desktop.ViewModels;

namespace Artemis.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (OperatingSystem.IsLinux())
        {
            ExtendClientAreaToDecorationsHint = false;
            WindowDecorations = WindowDecorations.Full;
        }
        else
        {
            ExtendClientAreaToDecorationsHint = true;
            WindowDecorations = WindowDecorations.None;
        }

        KeyDown += OnWindowKeyDown;
        PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DataContextProperty && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsCommandPaletteOpen) &&
                    viewModel.IsCommandPaletteOpen)
                {
                    PaletteSearchBox.Focus();
                }
            };
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsCommandPaletteOpen)
            return;

        if (e.Key == Key.Enter)
        {
            viewModel.ExecuteSelectedPaletteCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            viewModel.MovePaletteSelectionCommand.Execute(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            viewModel.MovePaletteSelectionCommand.Execute(-1);
            e.Handled = true;
        }
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnPaletteBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.CloseCommandPaletteCommand.Execute(null);
    }

    private void OnPaletteItemActivated(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.ExecuteSelectedPaletteCommand.Execute(null);
    }
}
