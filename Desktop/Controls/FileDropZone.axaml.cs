using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Artemis.Desktop.Controls;

public partial class FileDropZone : UserControl
{
    private bool _suppressClick;

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<FileDropZone, string>(nameof(Placeholder), "Drop files here");

    public static readonly StyledProperty<ICommand?> ClickCommandProperty =
        AvaloniaProperty.Register<FileDropZone, ICommand?>(nameof(ClickCommand));

    public static readonly StyledProperty<ICommand?> FilesDroppedCommandProperty =
        AvaloniaProperty.Register<FileDropZone, ICommand?>(nameof(FilesDroppedCommand));

    public FileDropZone()
    {
        InitializeComponent();
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDragEnterHandler(this, OnDragEnter);
        DragDrop.AddDragLeaveHandler(this, OnDragLeave);
        DragDrop.AddDropHandler(this, OnDrop);
        Zone.PointerReleased += OnZonePointerReleased;
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ICommand? ClickCommand
    {
        get => GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public ICommand? FilesDroppedCommand
    {
        get => GetValue(FilesDroppedCommandProperty);
        set => SetValue(FilesDroppedCommandProperty, value);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (HasFiles(e))
            Zone.Classes.Add("dragover");
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        Zone.Classes.Remove("dragover");
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        Zone.Classes.Remove("dragover");
        _suppressClick = true;

        IEnumerable<string> paths = GetDroppedPaths(e);
        if (FilesDroppedCommand?.CanExecute(paths) == true)
            FilesDroppedCommand.Execute(paths);

        e.Handled = true;
    }

    private void OnZonePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_suppressClick)
        {
            _suppressClick = false;
            return;
        }

        if (e.InitialPressMouseButton == MouseButton.Left && ClickCommand?.CanExecute(null) == true)
            ClickCommand.Execute(null);
    }

    private static bool HasFiles(DragEventArgs e) =>
        e.DataTransfer.Formats.Contains(DataFormat.File);

    private static IReadOnlyList<string> GetDroppedPaths(DragEventArgs e)
    {
        IEnumerable<IStorageItem>? files = e.DataTransfer.TryGetFiles();
        if (files is null)
            return [];

        return files
            .Select(file => file.TryGetLocalPath())
            .OfType<string>()
            .ToList();
    }
}
