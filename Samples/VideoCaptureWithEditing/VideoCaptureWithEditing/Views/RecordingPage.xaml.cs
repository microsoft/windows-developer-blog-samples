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

// See https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/CameraStarterKit for more examples

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using VideoCaptureWithEditing.Common;

namespace VideoCaptureWithEditing.Views
{
    public sealed partial class RecordingPage : Page
    {
        // Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
        // Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // Folder in which the captures will be stored (initialized in SetupUiAsync)
        private readonly StorageFolder localFolder;

        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest displayRequest;

        // For listening to media property changes
        private readonly SystemMediaTransportControls systemMediaControls;

        // MediaCapture and its state variables
        private MediaCapture mediaCapture;
        private bool isInitialized;
        private bool isPreviewing;
        private bool isRecording;

        // UI state
        private bool isSuspending;
        private bool isActivePage;
        private bool isUiActive;
        private Task setupTask = Task.CompletedTask;

        // Information about the camera device
        private bool mirroringPreview;
        private bool externalCamera;

        // Rotation Helper to simplify handling rotation compensation for the camera streams
        private CameraRotationHelper rotationHelper;

        public RecordingPage()
        {
            InitializeComponent();

            // For Visual Studio designer stability, we only want to use these when the app is actually running
            if (!DesignMode.DesignModeEnabled)
            {
                localFolder = ApplicationData.Current.LocalFolder;
                displayRequest = new DisplayRequest();
                systemMediaControls = SystemMediaTransportControls.GetForCurrentView();
            }

            NavigationCacheMode = NavigationCacheMode.Disabled;
        }
        
        #region MediaCapture methods

        /// <summary>
        /// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, starts preview and unlocks the UI
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCameraAsync()
        {
            Debug.WriteLine("InitializeCameraAsync");

            if (mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

                if (cameraDevice == null)
                {
                    Debug.WriteLine("No camera device found!");
                    return;
                }

                // Create MediaCapture and its settings
                mediaCapture = new MediaCapture();

                // Register for a notification when video recording has reached the maximum time and when something goes wrong
                mediaCapture.RecordLimitationExceeded += MediaCapture_RecordLimitationExceeded;
                mediaCapture.Failed += MediaCapture_Failed;

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                // Initialize MediaCapture
                try
                {
                    await mediaCapture.InitializeAsync(settings);
                    isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The app was denied access to the camera");
                }

                // If initialization succeeded, start the preview
                if (isInitialized)
                {
                    // Figure out where the camera is located
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                    {
                        // No information on the location of the camera, assume it's an external camera, not integrated on the device
                        externalCamera = true;
                    }
                    else
                    {
                        // Camera is fixed on the device
                        externalCamera = false;

                        // Only mirror the preview if the camera is on the front panel
                        mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }

                    // Initialize rotationHelper
                    rotationHelper = new CameraRotationHelper(cameraDevice.EnclosureLocation);
                    rotationHelper.OrientationChanged += RotationHelper_OrientationChanged;

                    await StartPreviewAsync();

                    UpdateCaptureControls();
                }
            }
        }

        /// <summary>
        /// Handles an orientation changed event
        /// </summary>
        private async void RotationHelper_OrientationChanged(object sender, bool updatePreview)
        {
            if (updatePreview)
            {
                await SetPreviewRotationAsync();
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateButtonOrientation());
        }

        /// <summary>
        /// Uses the current device orientation in space and page orientation on the screen to calculate the rotation
        /// transformation to apply to the controls
        /// </summary>
        private void UpdateButtonOrientation()
        {
            // Rotate the buttons in the UI to match the rotation of the device
            var angle = CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(rotationHelper.GetUiOrientation());
            var transform = new RotateTransform { Angle = angle };

            // The RenderTransform is safe to use (i.e. it won't cause layout issues) in this case, because these buttons have a 1:1 aspect ratio
            VideoButton.RenderTransform = transform;
        }

        /// <summary>
        /// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
        /// </summary>
        /// <returns></returns>
        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running
            displayRequest.RequestActive();

            // Set the preview source in the UI and mirror it if necessary
            PreviewControl.Source = mediaCapture;
            PreviewControl.FlowDirection = mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            // Start the preview
            await mediaCapture.StartPreviewAsync();
            isPreviewing = true;

            // Initialize the preview to the current orientation
            if (isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }

        /// <summary>
        /// Gets the current orientation of the UI in relation to the device (when AutoRotationPreferences cannot be honored) and applies a corrective rotation to the preview
        /// </summary>
        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device
            if (externalCamera) return;

            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var rotation = rotationHelper.GetCameraPreviewOrientation();
            var props = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(rotation));
            await mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
        }

        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            // Stop the preview
            isPreviewing = false;
            await mediaCapture.StopPreviewAsync();

            // Use the dispatcher because this method is sometimes called from non-UI threads
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Cleanup the UI
                PreviewControl.Source = null;

                // Allow the device screen to sleep now that the preview is stopped
                displayRequest.RequestRelease();
            });
        }
        
        /// <summary>
        /// Records an MP4 video to a StorageFile and adds rotation metadata to it
        /// </summary>
        /// <returns></returns>
        private async Task StartRecordingAsync()
        {
            try
            {

                // Create storage file for the capture
                var videoFile = await localFolder.CreateFileAsync($"Video.mp4", CreationCollisionOption.GenerateUniqueName);

                var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);

                // Calculate rotation angle, taking mirroring into account if necessary
                var rotationAngle = CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(rotationHelper.GetCameraCaptureOrientation());
                encodingProfile?.Video?.Properties.Add(RotationKey, PropertyValue.CreateInt32(rotationAngle));

                Debug.WriteLine("Starting recording to " + videoFile.Path);

                await mediaCapture.StartRecordToStorageFileAsync(encodingProfile, videoFile);

                isRecording = true;

                Debug.WriteLine("Started recording!");
            }
            catch (Exception ex)
            {
                // File I/O errors are reported as exceptions
                Debug.WriteLine("Exception when starting video recording: " + ex.ToString());
            }
        }

        /// <summary>
        /// Stops recording a video
        /// </summary>
        /// <returns></returns>
        private async Task StopRecordingAsync()
        {
            Debug.WriteLine("Stopping recording...");

            isRecording = false;

            await mediaCapture.StopRecordAsync();

            Debug.WriteLine("Stopped recording!");

            if(Frame.CanGoBack)
                Frame.GoBack();
        }

        /// <summary>
        /// Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync()
        {
            Debug.WriteLine("CleanupCameraAsync");

            if (isInitialized)
            {
                // If a recording is in progress during cleanup, stop it to save the recording
                if (isRecording)
                {
                    await StopRecordingAsync();
                }

                if (isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                isInitialized = false;
            }

            if (mediaCapture != null)
            {
                mediaCapture.RecordLimitationExceeded -= MediaCapture_RecordLimitationExceeded;
                mediaCapture.Failed -= MediaCapture_Failed;
                mediaCapture.Dispose();
                mediaCapture = null;
            }

            if (rotationHelper != null)
            {
                rotationHelper.OrientationChanged -= RotationHelper_OrientationChanged;
                rotationHelper = null;
            }
        }

        #endregion 
        
        #region Helper functions

        /// <summary>
        /// Initialize or clean up the camera and our UI,
        /// depending on the page state.
        /// </summary>
        /// <returns></returns>
        private async Task SetUpBasedOnStateAsync()
        {
            // Avoid reentrancy: Wait until nobody else is in this function.
            while (!setupTask.IsCompleted)
            {
                await setupTask;
            }

            // We want our UI to be active if
            // * We are the current active page.
            // * The window is visible.
            // * The app is not suspending.
            bool wantUiActive = isActivePage && Window.Current.Visible && !isSuspending;

            if (isUiActive != wantUiActive)
            {
                isUiActive = wantUiActive;

                Func<Task> setupAsync = async () =>
                {
                    if (wantUiActive)
                    {
                        await SetupUiAsync();
                        await InitializeCameraAsync();
                    }
                    else
                    {
                        await CleanupCameraAsync();
                        await CleanupUiAsync();
                    }
                };
                setupTask = setupAsync();
            }

            await setupTask;
        }

        /// <summary>
        /// Attempts to lock the page orientation, hide the StatusBar (on Phone) and registers event handlers for hardware buttons and orientation sensors
        /// </summary>
        /// <returns></returns>
        private async Task SetupUiAsync()
        {
            // Attempt to lock page to landscape orientation to prevent the CaptureElement from rotating, as this gives a better experience
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
            
            // Hide the status bar (NOTE: Be sure to add reference to 'Windows Mobile Extensions for the UWP')
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync();
            }

            RegisterEventHandlers();
            
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors, allows the StatusBar (on Phone) to show, and removes the page orientation lock
        /// </summary>
        /// <returns></returns>
        private async Task CleanupUiAsync()
        {
            UnregisterEventHandlers();

            // Show the status bar (NOTE: Be sure to add reference to 'Windows Mobile Extensions for the UWP')
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().ShowAsync();
            }

            // Revert orientation preferences
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
        }

        /// <summary>
        /// This method will update the icons, enable/disable and show/hide the photo/video buttons depending on the current state of the app and the capabilities of the device
        /// </summary>
        private void UpdateCaptureControls()
        {
            // The buttons should only be enabled if the preview started sucessfully
            VideoButton.IsEnabled = isPreviewing;

            // Update recording button to show "Stop" icon instead of red "Record" icon
            StartRecordingIcon.Visibility = isRecording ? Visibility.Collapsed : Visibility.Visible;
            StopRecordingIcon.Visibility = isRecording ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
        /// </summary>
        private void RegisterEventHandlers()
        {
            systemMediaControls.PropertyChanged += SystemMediaControls_PropertyChanged;
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors
        /// </summary>
        private void UnregisterEventHandlers()
        {
            systemMediaControls.PropertyChanged -= SystemMediaControls_PropertyChanged;
        }

        /// <summary>
        /// Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
        /// </summary>
        /// <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
        /// <returns></returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        #endregion

        #region Event handlers

        private async void VideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                await StartRecordingAsync();
            }
            else
            {
                await StopRecordingAsync();
            }

            // After starting or stopping video recording, update the UI to reflect the MediaCapture state
            UpdateCaptureControls();
        }

        private async void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs args)
        {
            await SetUpBasedOnStateAsync();
        }

        /// <summary>
        /// In the event of the app being minimized this method handles media property change events. If the app receives a mute
        /// notification, it is no longer in the foregroud.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void SystemMediaControls_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                // Only handle this event if this page is currently being displayed
                if (args.Property == SystemMediaTransportControlsProperty.SoundLevel && Frame.CurrentSourcePageType == typeof(MainPage))
                {
                    // Check to see if the app is being muted. If so, it is being minimized.
                    // Otherwise if it is not initialized, it is being brought into focus.
                    if (sender.SoundLevel == SoundLevel.Muted)
                    {
                        await CleanupCameraAsync();
                    }
                    else if (!isInitialized)
                    {
                        await InitializeCameraAsync();
                    }
                }
            });
        }

        private async void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            // This is a notification that recording has to stop, and the app is expected to finalize the recording

            await StopRecordingAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);

            await CleanupCameraAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        #endregion

        #region Page and application lifecycle

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Useful to know when to initialize/clean up the camera, so we hook into app suspenstion and window visibility events
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;

            isActivePage = true;
            await SetUpBasedOnStateAsync();
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Unhook event handlers
            Application.Current.Suspending -= Application_Suspending;
            Application.Current.Resuming -= Application_Resuming;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;

            isActivePage = false;
            await SetUpBasedOnStateAsync();
        }

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            isSuspending = false;

            var deferral = e.SuspendingOperation.GetDeferral();
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
            {
                await SetUpBasedOnStateAsync();
                deferral.Complete();
            });
        }

        private async void Application_Resuming(object sender, object o)
        {
            isSuspending = false;

            await Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
            {
                await SetUpBasedOnStateAsync();
            });
        }

        #endregion
    }
}