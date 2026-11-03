using System.Windows.Controls;

namespace BlackSite.App.Views;

public partial class FutureModuleView : UserControl
{
    public FutureModuleView(string glyph, string title, string body)
    {
        InitializeComponent();
        IconText.Text = glyph;
        TitleText.Text = title.ToUpperInvariant();
        BodyText.Text = body;
    }
}
