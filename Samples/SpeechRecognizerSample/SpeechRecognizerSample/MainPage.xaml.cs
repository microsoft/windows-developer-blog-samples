using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.SpeechRecognition;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SpeechRecognizerSample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        async Task<string> GetSpeechTextAsync()
        {
            
            string text = string.Empty;

            using (SpeechRecognizer recognizer = new SpeechRecognizer())
            {
                await recognizer.CompileConstraintsAsync();

                SpeechRecognitionResult result = await recognizer.RecognizeWithUIAsync();

                if (result.Status == SpeechRecognitionResultStatus.Success)
                {
                    text = result.Text;
                }
            }
            return (text);
        }

        private async void btnSpeech_Click(object sender, RoutedEventArgs e)
        {
            // Use speech recognizer to obtain input from user
            var text = await GetSpeechTextAsync();

            // Set txtInput to the text entered from user
            txtInput.Text = String.IsNullOrEmpty(text) ? "No text input was received." :
                            String.Format("Text received: {0}", text);
        }
    }



}
