using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BlackSite.App.Services;

namespace BlackSite.App.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText, string glyph, bool danger)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        IconText.Text = glyph;
        if (danger) IconText.Foreground = (Brush)FindResource("RedBrush");
        else ConfirmButton.Style = (Style)FindResource("HudButton");
        Loaded += (_, _) => CancelButton.Focus();
    }

    /// <summary>Shows the dialog modally; true only if the user pressed Confirm.</summary>
    public static bool Ask(Window? owner, string title, string message, string confirmText = "CONFIRM", string glyph = "", bool danger = true)
    {
        BlackSite.Core.Logging.Log.Info($"Confirm dialog: {title}");
        AudioManager.Play(SoundKind.Warning);
        var d = new ConfirmDialog(title, message, confirmText, glyph, danger);
        if (owner is { IsVisible: true }) d.Owner = owner; else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return d.ShowDialog() == true;
    }

    private void Drag(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Confirm_Click(object sender, RoutedEventArgs e) { DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) DialogResult = false;
    }
}
