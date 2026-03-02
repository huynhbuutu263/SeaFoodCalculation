using SeafoodVision.Presentation.ViewModels;
using System.Windows;

namespace SeafoodVision.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
