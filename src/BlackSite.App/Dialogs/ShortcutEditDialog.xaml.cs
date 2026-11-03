using System.Windows;
using System.Windows.Input;
using BlackSite.Core.Shortcuts;
using Microsoft.Win32;

namespace BlackSite.App.Dialogs;

public partial class ShortcutEditDialog : Window
{
    private readonly ShortcutDefinition _original;

    /// <summary>The validated shortcut after the user pressed Save.</summary>
    public ShortcutDefinition? Result { get; private set; }

    public ShortcutEditDialog(ShortcutDefinition? existing)
    {
        InitializeComponent();
        _original = existing?.Clone() ?? new ShortcutDefinition { Glyph = "" };
        HeadText.Text = existing is null ? "ADD SHORTCUT" : $"EDIT SHORTCUT — {existing.Name}";
        NameBox.Text = _original.Name;
        DescBox.Text = _original.Description;
        // Show the first available target for built-ins (e.g. the resolved Edge path), else the primary one.
        var shown = existing is null ? null : new ApplicationLauncher().Resolve(existing) ?? existing.Primary;
        TargetBox.Text = shown?.Value ?? "";
        IconBox.Text = _original.IconPath;
        Loaded += (_, _) => (string.IsNullOrEmpty(NameBox.Text) ? NameBox : TargetBox).Focus();
    }

    private void Drag(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Choose a program", Filter = "Programs (*.exe;*.lnk;*.msc;*.cpl)|*.exe;*.lnk;*.msc;*.cpl|All files|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        TargetBox.Text = dlg.FileName;
        if (string.IsNullOrWhiteSpace(NameBox.Text)) NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Choose an icon", Filter = "Images and programs|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.exe|All files|*.*" };
        if (dlg.ShowDialog(this) == true) IconBox.Text = dlg.FileName;
    }

    internal static LaunchTarget ParseTarget(string text)
    {
        text = text.Trim().Trim('"');
        bool isUri = System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z][A-Za-z0-9\-]+:(?!\\|/(?!/))") && !System.IO.Path.IsPathRooted(text);
        return new LaunchTarget(isUri ? TargetKind.Uri : TargetKind.Executable, text);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = _original;
        s.Name = NameBox.Text.Trim();
        s.Description = DescBox.Text.Trim();
        s.IconPath = IconBox.Text.Trim();
        s.Targets = new List<LaunchTarget> { ParseTarget(TargetBox.Text) };

        var r = ShortcutValidator.ValidateShortcut(s, requireExists: true);
        if (!r.IsValid) { ErrorText.Text = r.Message; return; }
        if (s.IconPath.Length > 0 && !System.IO.File.Exists(Environment.ExpandEnvironmentVariables(s.IconPath)))
        {
            ErrorText.Text = "Icon file not found.";
            return;
        }
        Result = s;
        DialogResult = true;
    }
}
