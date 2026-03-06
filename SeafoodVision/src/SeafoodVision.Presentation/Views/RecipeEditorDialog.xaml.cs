using System.Windows;
using SeafoodVision.Presentation.ViewModels;

namespace SeafoodVision.Presentation.Views;

/// <summary>
/// Interaction logic for RecipeEditorDialog.xaml
/// </summary>
public partial class RecipeEditorDialog : Window
{
    private readonly RecipeEditorViewModel _viewModel;

    public RecipeEditorDialog(RecipeEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        
        Closed += (s, e) => _viewModel.Dispose();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
}
