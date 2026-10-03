using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Threading;

namespace CanvaStage.Windows.Views;

public partial class ConfidencePreview : UserControl
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(15) };

    public ConfidencePreview()
    {
        InitializeComponent();
        Loaded += (_, _) => { UpdateClock(); _clock.Start(); };
        Unloaded += (_, _) => _clock.Stop();
        _clock.Tick += (_, _) => UpdateClock();
    }

    private void UpdateClock() => ClockText.Text = DateTime.Now.ToString("h:mm tt");
}
