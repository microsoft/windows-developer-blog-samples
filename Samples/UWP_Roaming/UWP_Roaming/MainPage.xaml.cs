using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWP_Roaming
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        StorageFolder roamingFolder = null;
        int roamingCounter = 0;
        const string filename = "sampleFile.txt";

        public MainPage()
        {
            this.InitializeComponent();
            roamingFolder = ApplicationData.Current.RoamingFolder;
            Read_Roaming_Counter();
        }


        async void Increment_Click(object sender, RoutedEventArgs e)
        {
            roamingCounter++;

            StorageFile file = await roamingFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, roamingCounter.ToString());

            Read_Roaming_Counter();
        }

        async void Read_Roaming_Counter()
        {
            try
            {
                StorageFile file = await roamingFolder.GetFileAsync(filename);
                string text = await FileIO.ReadTextAsync(file);

                CounterResult.Text = "Roaming Counter: " + text;

                roamingCounter = int.Parse(text);
            }
            catch (Exception)
            {
                CounterResult.Text = "Roaming Counter: <not found>";
            }
        }
    }
}
