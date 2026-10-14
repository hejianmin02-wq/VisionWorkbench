using System.Windows;

namespace VisionWorkbench.Desktop;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Closed += (_, _) => ViewModel.RequestCancellation();
    }
}
