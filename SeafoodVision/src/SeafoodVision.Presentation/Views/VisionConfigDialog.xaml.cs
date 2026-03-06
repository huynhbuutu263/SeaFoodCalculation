using System.Windows;
using SeafoodVision.Presentation.ViewModels;

namespace SeafoodVision.Presentation.Views;

/// <summary>
/// Interaction logic for VisionConfigDialog.xaml
/// </summary>
public partial class VisionConfigDialog : Window
{
    public VisionConfigDialog(VisionConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Cleanly execute disposal handles if the window is closed directly by user
        Closed += (s, e) => viewModel.Dispose();
    }
}
