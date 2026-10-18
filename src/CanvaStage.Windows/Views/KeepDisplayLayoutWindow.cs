using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;

namespace CanvaStage.Windows.Views;

public sealed class KeepDisplayLayoutWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly System.Diagnostics.Stopwatch _elapsed = new();
    private readonly TimeSpan _timeout;
    private readonly TextBlock _countdown = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 18) };
    public KeepDisplayLayoutWindow() : this(TimeSpan.FromSeconds(15)) { }
    internal KeepDisplayLayoutWindow(TimeSpan timeout)
    {
        _timeout = timeout;
        Title = "Keep mirroring these screens?";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("SurfaceBrush");
        Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("PrimaryTextBrush");
        Owner = System.Windows.Application.Current.MainWindow;
        StackPanel panel = new() { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Can you see the audience slides on both screens?", TextWrapping = TextWrapping.Wrap, FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(_countdown);
        StackPanel buttons = new() { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        Button yes = new() { Content = "Yes", Style = (Style)FindResource("PrimaryButton"), MinWidth = 80 };
        Button no = new() { Content = "No", Style = (Style)FindResource("SecondaryButton"), MinWidth = 80, IsDefault = true, IsCancel = true };
        yes.Click += (_, _) => DialogResult = true;
        no.Click += (_, _) => DialogResult = false;
        buttons.Children.Add(yes);
        buttons.Children.Add(no);
        panel.Children.Add(buttons);
        Content = panel;
        _timer.Tick += OnTick;
        Closed += (_, _) => { _timer.Stop(); _timer.Tick -= OnTick; };
        UpdateCountdown();
        Loaded += (_, _) => { _elapsed.Start(); _timer.Start(); };
    }
    private void OnTick(object? sender, EventArgs args)
    {
        UpdateCountdown();
        if (_elapsed.Elapsed >= _timeout) DialogResult = false;
    }
    private void UpdateCountdown()
    {
        int seconds = Math.Max(0, (int)Math.Ceiling((_timeout - _elapsed.Elapsed).TotalSeconds));
        _countdown.Text = $"Choose Yes to keep mirroring, or No to restore the previous layout now. With no answer, it restores in {seconds} {(seconds == 1 ? "second" : "seconds")}. Confidence output stays stopped.";
    }
}
