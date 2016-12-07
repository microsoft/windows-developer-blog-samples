using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PatientChart
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            // Set up the InkCanvas being used for handwriting recognition
            NameInkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen;
            
            NameInkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(new InkDrawingAttributes
            {
                Color = Windows.UI.Colors.Black,
                IgnorePressure = true,
                FitToCurve = true
            });

            // Setup the doctor's notes InkCanvas
            NotesInkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen;

            NotesInkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(new InkDrawingAttributes
            {
                IgnorePressure = false,
                FitToCurve = true
            });
        }

        private async void RecognizeHandwritingButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Get all strokes on the InkCanvas.
            var currentStrokes = NameInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            // Ensure an ink stroke is present.
            if (currentStrokes.Count < 1)
            {
                await new MessageDialog("You have not written anything in the canvas area").ShowAsync();
                return;
            }

            // Create a manager for the InkRecognizer object used in handwriting recognition.
            var inkRecognizerContainer = new InkRecognizerContainer();

            // inkRecognizerContainer is null if a recognition engine is not available.
            if (inkRecognizerContainer == null)
            {
                await new MessageDialog("You must install handwriting recognition engine.").ShowAsync();
                return;
            }

            // Recognize all ink strokes on the ink canvas.
            var recognitionResults = await inkRecognizerContainer.RecognizeAsync(
                    NameInkCanvas.InkPresenter.StrokeContainer,
                    InkRecognitionTarget.All);

            // Process and display the recognition results.
            if (recognitionResults.Count < 1)
            {
                await new MessageDialog("No recognition results.").ShowAsync();
                return;
            }

            var str = "";

            // Iterate through the recognition results, this will loop once for every word detected
            foreach (var result in recognitionResults)
            {
                // Get all recognition candidates from each recognition result
                var candidates = result.GetTextCandidates();

                // For the purposes of this demo, we'll use the first result
                var recognizedName = candidates[0];

                // Concatenate the results
                str += recognizedName + " ";
            }

            // Display the recognized name
            PatientNameTextBlock.Text = str.Trim();

            // Clear the ink canvas once recognition is complete.
            NameInkCanvas.InkPresenter.StrokeContainer.Clear();
        }

        private async void SaveChartButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Get all strokes on the NotesInkCanvas.
            var currentStrokes = NotesInkCanvas.InkPresenter.StrokeContainer.GetStrokes();

            // Strokes present on ink canvas.
            if (currentStrokes.Count > 0)
            {
                // Initialize the picker.
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("GIF with embedded ISF", new List<string>() { ".gif" });
                savePicker.DefaultFileExtension = ".gif";

                // We use the patient's name to suggest a file name
                savePicker.SuggestedFileName = $"{PatientNameTextBlock.Text} Chart";
                
                // Show the file picker.
                var file = await savePicker.PickSaveFileAsync();

                if (file != null)
                {
                    // Prevent updates to the file until updates are finalized with call to CompleteUpdatesAsync.
                    CachedFileManager.DeferUpdates(file);

                    // Open a file stream for writing
                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    using (var outputStream = stream.GetOutputStreamAt(0))
                    {
                        await NotesInkCanvas.InkPresenter.StrokeContainer.SaveAsync(outputStream);
                        await outputStream.FlushAsync();
                    }

                    // Finalize write so other apps can update file.
                    var status = await CachedFileManager.CompleteUpdatesAsync(file);

                    if (status == FileUpdateStatus.Complete)
                    {
                        PatientNameTextBlock.Text += " (saved!)";
                    }
                }
            }
        }
    }
}
