using Windows.UI.Xaml.Controls;
using HighPerformanceDataBinding.ViewModels;

namespace HighPerformanceDataBinding
{
    public sealed partial class MainPage : Page
    {
        // Alternatively, you can do this to get a ViewModel reference
        // private MainPageViewModel ViewModel => this.DataContext as MainPageViewModel;

        public MainPage()
        {
            InitializeComponent();
        }
    }
}
