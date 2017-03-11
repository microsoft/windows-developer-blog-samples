using Windows.UI;
using Windows.UI.Xaml.Media;
using HighPerformanceDataBinding.ViewModels;

namespace HighPerformanceDataBinding.Models
{
    // Book.cs
    public class Book : ViewModelBase
    {
        private string title;
        private double price;
        private string coverImageUrl;

        public string Title
        {
            get { return title; }
            set { title = value; OnPropertyChanged(); }
        }

        public double Price
        {
            get { return price; }
            set { price = value; OnPropertyChanged(); }
        }

        public string CoverImageUrl
        {
            get { return coverImageUrl; }
            set { coverImageUrl = value; OnPropertyChanged(); }
        }

        public SolidColorBrush GetPriceForeground(double p)
        {
            return p > 3 ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);
        }
    }
}
