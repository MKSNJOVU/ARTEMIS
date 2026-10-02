using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Artemis.Desktop.Controls;

public partial class FileChipList : UserControl
{
    public static readonly StyledProperty<IEnumerable?> FilesProperty =
        AvaloniaProperty.Register<FileChipList, IEnumerable?>(nameof(Files));

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<FileChipList, ICommand?>(nameof(RemoveCommand));

    public FileChipList()
    {
        InitializeComponent();
    }

    public IEnumerable? Files
    {
        get => GetValue(FilesProperty);
        set => SetValue(FilesProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }
}
