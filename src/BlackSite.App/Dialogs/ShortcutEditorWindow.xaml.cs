using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BlackSite.Core.Shortcuts;

namespace BlackSite.App.Dialogs;

public partial class ShortcutEditorWindow : Window
{
    private sealed record Row(string Id, string Glyph, string Name, string Target, string Status, Brush StatusBrush);

    private readonly ShortcutManager _mgr;
    private readonly ApplicationLauncher _launcher = new();
    private Point _dragStart;
    private Row? _dragRow;

    public ShortcutEditorWindow(ShortcutManager mgr)
    {
        InitializeComponent();
        _mgr = mgr;
        Reload();
    }

    private void Reload(string? select = null)
    {
        var ok = (Brush)FindResource("GreenBrush");
        var bad = (Brush)FindResource("RedBrush");
        List.ItemsSource = _mgr.Items.Select(s =>
        {
            var t = _launcher.Resolve(s);
            return new Row(s.Id, s.Glyph, s.Name, (t ?? s.Primary)?.ToString() ?? "", t is null ? "UNAVAILABLE" : "OK", t is null ? bad : ok);
        }).ToList();
        if (select is not null) List.SelectedItem = ((List<Row>)List.ItemsSource).FirstOrDefault(r => r.Id == select);
    }

    private void Drag(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ShortcutEditDialog(null) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is null) return;
        var r = _mgr.Add(dlg.Result);
        if (!r.IsValid) MessageBox.Show(this, r.Message, "BLACKSITE", MessageBoxButton.OK, MessageBoxImage.Warning);
        Reload(dlg.Result.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not Row row) return;
        var current = _mgr.Items.FirstOrDefault(s => s.Id == row.Id);
        if (current is null) return;
        var dlg = new ShortcutEditDialog(current) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is null) return;
        var r = _mgr.Replace(row.Id, dlg.Result);
        if (!r.IsValid) MessageBox.Show(this, r.Message, "BLACKSITE", MessageBoxButton.OK, MessageBoxImage.Warning);
        Reload(row.Id);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not Row row) return;
        if (!ConfirmDialog.Ask(this, "Remove shortcut?", $"Remove \"{row.Name}\" from the dashboard?", "REMOVE", "", true)) return;
        _mgr.Remove(row.Id);
        Reload();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Ask(this, "Restore default shortcuts?", "This replaces your list with the 10 built-in application shortcuts.", "RESTORE", "", false)) return;
        _mgr.RestoreDefaults();
        Reload();
    }

    // ---- drag & drop reorder ----
    private void List_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragRow = (e.OriginalSource as DependencyObject) is { } d ? ItemAt(d) : null;
    }

    private static Row? ItemAt(DependencyObject d)
    {
        while (d is not null and not System.Windows.Controls.ListBoxItem) d = VisualTreeHelper.GetParent(d);
        return (d as System.Windows.Controls.ListBoxItem)?.DataContext as Row;
    }

    private void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragRow is null) return;
        var diff = e.GetPosition(null) - _dragStart;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var row = _dragRow;
        _dragRow = null;
        DragDrop.DoDragDrop(List, new DataObject("bs-row", row), DragDropEffects.Move);
    }

    private void List_DragOver(object sender, DragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent("bs-row") ? DragDropEffects.Move : DragDropEffects.None;

    private void List_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData("bs-row") is not Row src) return;
        var target = e.OriginalSource is DependencyObject d ? ItemAt(d) : null;
        var items = (List<Row>)List.ItemsSource;
        int from = items.FindIndex(r => r.Id == src.Id);
        int to = target is null ? items.Count - 1 : items.FindIndex(r => r.Id == target.Id);
        _mgr.Move(from, to);
        Reload(src.Id);
    }
}
