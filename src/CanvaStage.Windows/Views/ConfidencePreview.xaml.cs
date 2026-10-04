using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Media;

namespace CanvaStage.Windows.Views;

public partial class ConfidencePreview : UserControl
{
    public ConfidencePreview() => InitializeComponent();

    internal Visual? SourceVisual => OutputBrush.Visual;
}
