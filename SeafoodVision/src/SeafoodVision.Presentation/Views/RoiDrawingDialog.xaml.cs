using System.Windows;
using SeafoodVision.Presentation.ViewModels;

namespace SeafoodVision.Presentation.Views;

/// <summary>
/// Interaction logic for RoiDrawingDialog.xaml
/// Coordinates the dialog lifespan via the ViewModel.
/// </summary>
public partial class RoiDrawingDialog : Window
{
    public RoiDrawingDialog(RoiDrawingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Hook up the close signal from the viewmodel
        viewModel.CloseDialogAction = () =>
        {
            DialogResult = viewModel.IsConfirmed;
            Close();
        };
    }
}
