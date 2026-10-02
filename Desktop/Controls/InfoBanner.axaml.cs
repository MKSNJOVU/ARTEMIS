using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Artemis.Desktop.Models;

namespace Artemis.Desktop.Controls;

public partial class InfoBanner : UserControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<InfoBanner, string?>(nameof(Message));

    public static readonly StyledProperty<NotificationKind> KindProperty =
        AvaloniaProperty.Register<InfoBanner, NotificationKind>(nameof(Kind), NotificationKind.Info);

    public static readonly StyledProperty<string?> ActionContentProperty =
        AvaloniaProperty.Register<InfoBanner, string?>(nameof(ActionContent));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<InfoBanner, ICommand?>(nameof(ActionCommand));

    public static readonly StyledProperty<bool> ShowActionProperty =
        AvaloniaProperty.Register<InfoBanner, bool>(nameof(ShowAction));

    public static readonly StyledProperty<bool> IsBannerVisibleProperty =
        AvaloniaProperty.Register<InfoBanner, bool>(nameof(IsBannerVisible));

    public InfoBanner()
    {
        InitializeComponent();
        MessageProperty.Changed.AddClassHandler<InfoBanner>((banner, _) => banner.Refresh());
        KindProperty.Changed.AddClassHandler<InfoBanner>((banner, _) => banner.Refresh());
        ActionContentProperty.Changed.AddClassHandler<InfoBanner>((banner, _) => banner.Refresh());
        Loaded += (_, _) => Refresh();
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public NotificationKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public bool ShowAction
    {
        get => GetValue(ShowActionProperty);
        set => SetValue(ShowActionProperty, value);
    }

    public bool IsBannerVisible
    {
        get => GetValue(IsBannerVisibleProperty);
        set => SetValue(IsBannerVisibleProperty, value);
    }

    private void Refresh()
    {
        IsBannerVisible = !string.IsNullOrWhiteSpace(Message);
        ShowAction = !string.IsNullOrWhiteSpace(ActionContent) && ActionCommand?.CanExecute(null) == true;

        IBrush? background = Kind switch
        {
            NotificationKind.Success => this.FindResource("ArtemisSuccessSoftBrush") as IBrush,
            NotificationKind.Error => this.FindResource("ArtemisDangerSoftBrush") as IBrush,
            NotificationKind.Warning => this.FindResource("ArtemisWarningSoftBrush") as IBrush,
            _ => this.FindResource("ArtemisAccentSoftBrush") as IBrush
        };
        IBrush? border = Kind switch
        {
            NotificationKind.Success => this.FindResource("ArtemisSuccessBrush") as IBrush,
            NotificationKind.Error => this.FindResource("ArtemisDangerBrush") as IBrush,
            NotificationKind.Warning => this.FindResource("ArtemisWarningBrush") as IBrush,
            _ => this.FindResource("ArtemisAccentBrush") as IBrush
        };

        Banner.Background = background;
        Banner.BorderBrush = border;
    }
}
