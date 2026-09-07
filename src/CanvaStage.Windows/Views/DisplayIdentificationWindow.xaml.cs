using System.Windows;

namespace CanvaStage.Windows.Views;

public partial class DisplayIdentificationWindow : Window
{
    public DisplayIdentificationWindow(DisplayOptionViewModel display)
    {
        InitializeComponent();
        DataContext = display;
    }
}

public sealed record DisplayOptionViewModel(int Number, string FriendlyName, string Resolution);
