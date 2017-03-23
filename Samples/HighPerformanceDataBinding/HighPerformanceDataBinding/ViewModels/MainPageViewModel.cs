using System.Collections.ObjectModel;
using Windows.UI.Xaml.Controls;
using HighPerformanceDataBinding.Models;

namespace HighPerformanceDataBinding.ViewModels
{
    // MainPageViewModel.cs
    public class MainPageViewModel
    {
        public MainPageViewModel()
        {
            Books = new ObservableCollection<Book>();

            for (var i = 0; i < 300; i++)
            {
                Books.Add(new Book
                {
                    Title = $"Book {i}",
                    Price = i * i * 0.01,
                    CoverImageUrl = "ms-appx:///Assets/Images/ButterflyWorld_1920x1200.jpg"
                });
            }
        }

        public ObservableCollection<Book> Books { get; set; }

        public void ListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e?.AddedItems.Count > 0)
                (e.AddedItems[0] as Book).Price++;
        }
    }
}
