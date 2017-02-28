//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

// See https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/MediaEditing for more examples

using System;
using System.Diagnostics;
using System.Linq;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace VideoCaptureWithEditing.Views
{
    public sealed partial class EditingPage : Page
    {
        private MediaComposition composition;
        private MediaStreamSource mediaStreamSource;

        public EditingPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var videoFile = e.Parameter as StorageFile;

            if (videoFile != null)
            {
                StatusTextBlock.Text = videoFile.DisplayName;

                // Create a MediaClip from the file
                var clip = await MediaClip.CreateFromFileAsync(videoFile);

                // Set the End Trim slider's maximum value so that the user can trim from the end
                // You can also do this from the start
                EndTrimSlider.Maximum = clip.OriginalDuration.TotalMilliseconds;

                // Create a MediaComposition containing the clip and set it on the MediaElement.
                composition = new MediaComposition();
                composition.Clips.Add(clip);

                // start the MediaElement at the beginning
                EditorMediaElement.Position = TimeSpan.Zero;

                // Create the media source and assign it to the media player
                mediaStreamSource = composition.GeneratePreviewMediaStreamSource((int)EditorMediaElement.ActualWidth, (int)EditorMediaElement.ActualHeight);

                // Set the MediaElement's source
                EditorMediaElement.SetMediaStreamSource(mediaStreamSource);

                TrimClipButton.IsEnabled = true;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Clean up
            EditorMediaElement.Source = null;
            composition = null;
            mediaStreamSource = null;
            base.OnNavigatedFrom(e);
        }

        private void EndTrimSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            StatusTextBlock.Text = $"Click TRIM button to cut at the {TimeSpan.FromMilliseconds(e.NewValue):g} mark.";
        }

        private void TrimClip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show the overlay that contains the ProgressBar
                BusyOverlay.Visibility = Visibility.Visible;
                EncodingProgressTextBlock.Text = "trimming...";

                // Get the first clip in the MediaComposition
                // We know this beforehand because it's the only clip in the composition
                // that we created from the passed video file
                MediaClip clip = composition.Clips.FirstOrDefault();

                // Trim the end of the clip (you can use TrimTimeFromStart to trim from the beginning)
                clip.TrimTimeFromEnd = TimeSpan.FromMilliseconds((long) EndTrimSlider.Value);

                // Rewind the MediaElement
                EditorMediaElement.Position = TimeSpan.Zero;

                // Update the video source with the trimmed clip
                mediaStreamSource = composition.GeneratePreviewMediaStreamSource((int) EditorMediaElement.ActualWidth, (int) EditorMediaElement.ActualHeight);

                // Set the MediaElement's source
                EditorMediaElement.SetMediaStreamSource(mediaStreamSource);

                // Update the UI
                EndTrimSlider.Value = 0;
                StatusTextBlock.Text = "Trim Successful! Trim again or click the SAVE button to keep.";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                SaveButton.IsEnabled = true;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
                throw;
            }
            finally
            {
                BusyOverlay.Visibility = Visibility.Collapsed;
                EncodingProgressTextBlock.Text = "";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            EnableButtons(false);
            StatusTextBlock.Text = "Creating new file...";
            
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync($"Edited Video.mp4", CreationCollisionOption.GenerateUniqueName);
            
            if (file != null)
            {
                // Show the overlay that contains the ProgressBar
                BusyOverlay.Visibility = Visibility.Visible;

                // You can also use a faster option with MediaTrimmingPreference.Fast, but will have a lower quality trim
                var saveOperation = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise);

                // This will show progress as video is rendered and saved
                saveOperation.Progress = async (info, progress) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        EncodingProgressBar.Value = progress;
                        EncodingProgressTextBlock.Text = $"Saving file: {progress:F0}%";
                    });
                };

                // This fires when the operation is complete
                saveOperation.Completed = async (info, status) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            var results = info.GetResults();

                            if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
                            {
                                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                                StatusTextBlock.Text = "Saving was unsuccessful";
                            }
                            else
                            {
                                // Successful save, go back to main page.
                                if (Frame.CanGoBack)
                                    Frame.GoBack();
                            }
                        }
                        finally
                        {
                            // Remember to re-enable controls on both success and failure
                            EnableButtons(true);
                            BusyOverlay.Visibility = Visibility.Collapsed;
                            EncodingProgressTextBlock.Text = "";
                        }
                    });
                };
            }
            else
            {
                EnableButtons(true);
                BusyOverlay.Visibility = Visibility.Collapsed;
                EncodingProgressTextBlock.Text = "";
            }
        }

        private void EnableButtons(bool isEnabled)
        {
            SaveButton.IsEnabled = isEnabled;
            TrimClipButton.IsEnabled = isEnabled;
        }
    }
}
