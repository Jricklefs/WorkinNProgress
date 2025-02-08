//using Microsoft.ML.OnnxRuntime;
//using Microsoft.ML.OnnxRuntime.Tensors;
using DartDetection.Model_Logic;
using DartDetection.Utilities;
using Microsoft.ML;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Tensorflow.Operations.Initializers;

namespace DartDetection
{
    /// <summary>
    /// CameraWindow class handles video capturing, baseline image comparison, and saving logic for up to 4 cameras.
    /// </summary>
    public partial class CameraWindow : System.Windows.Window
    {
        private VideoCapture[] _captures; // Manages video capture for each camera.
        private Mat[] _frames; // Stores current frames from each camera.
        private BitmapSource[] _bitmaps; // Converted bitmaps for displaying camera feeds in UI.
        private Mat[] _baselineImages; // Baseline images for each camera.
        private Mat[] _calibratedBaselineImages; // Calibrated baseline images for comparison.
        private bool _isCapturing; // Controls video capturing loop.
        private Mat[] _capturedFrames; // Frames to be saved.
        private bool _isComparing = false; // Toggles image comparison.
        private bool _isSaving = false; // Indicates image saving is in progress.
        private bool _compareToCalibratedBaseline = false; // Determines comparison against calibrated baseline.
        private bool _allowPromptToSave = true; // Controls whether to prompt for saving.
        private int MaxCameras = 4; // Maximum number of cameras supported.
        private double _zoomFactor = 1.0; // Zoom factor for camera feed.
        private int _saveCounter = 0; // Tracks how many images have been saved.
        //private SamOnnxInference _samInference; // SAM ONNX model for inference tasks.
        private BaselineImageWindow _baselineImageWindow; // Window for displaying baseline images.
        //private DifferenceWindow _differenceWindow; // Window for displaying image differences.

        private List<string> _trackingData; // Array to store difference values and save events.
        private bool _enableTracking = false; // Flag to enable or disable tracking.
        private string _lastTrackingValue = string.Empty; // Store the last added value


        private System.Threading.ManualResetEvent _pauseEvent = new System.Threading.ManualResetEvent(true); // Pauses/resumes comparison.
        private PredictionService _predictionService;
        private readonly object _saveLock = new object(); // Lock object for synchronization
        private readonly bool _useOnxx  = true;
        /// <summary>
        /// Initializes CameraWindow, setting up UI and loading required resources.
        /// </summary>
        public CameraWindow()
        {
            InitializeComponent();
            Loaded += CameraWindow_Loaded; // Event handler for when the window is loaded.
            Closed += CameraWindow_Closed; // Event handler for when the window is closed.

            // Initialize the baseline image and difference windows.
            _baselineImageWindow = new BaselineImageWindow();
            _baselineImageWindow.Show();

            _trackingData = new List<string>(); // Initialize the tracking array.

            string modelPath;
            if (!_useOnxx)
            {
                modelPath = Path.Combine("AIModels", "frozen", "frozen_model.pb");
            }
            else
            {
                modelPath = Path.Combine("AIModels", "dartSegGS.onnx");
                //modelPath = Path.Combine("AIModels", "output_model.onnx");
            }


            _predictionService = new PredictionService(modelPath);


            // _differenceWindow = new DifferenceWindow();

            // Load SAM ONNX model from file.
            //string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OnnxModel", "sam_vit_h.onnx");
            //_samInference = new SamOnnxInference(modelPath);
        }

        /// <summary>
        /// Event handler triggered when the window is loaded. Initializes the cameras and starts video capturing.
        /// </summary>
        private void CameraWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _captures = new VideoCapture[MaxCameras];
            _frames = new Mat[MaxCameras];
            _bitmaps = new BitmapSource[MaxCameras];
            _capturedFrames = new Mat[MaxCameras];
            _baselineImages = new Mat[MaxCameras];
            _calibratedBaselineImages = new Mat[MaxCameras];


            ImageProcessingHelper.LoadCalibrationSettings();

            for (int i = 0; i < MaxCameras; i++)
            {
                _captures[i] = new VideoCapture(i);
                if (!_captures[i].IsOpened())
                {
                    _captures[i] = null;
                    break;
                }
                _frames[i] = new Mat();
            }

            _isCapturing = true;
            var captureThread = new Thread(CaptureVideo); // Start capturing in a new thread.
            captureThread.Start();
        }

        /// <summary>
        /// Event handler for capturing and saving images when the "Capture Image" button is clicked.
        /// </summary>
        private void CaptureImage_Click(object sender, RoutedEventArgs e)
        {
            if (_allowPromptToSave)
            {
                CaptureAndSaveImages(true); // Capture and save images with user prompt.
            }
        }
        private async void UploadImage_Click(object sender, RoutedEventArgs e)
        {
            // Create an OpenFileDialog to let the user select an image
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";

            if (openFileDialog.ShowDialog() == true)
            {
                

                string imagePath = openFileDialog.FileName;
                bool dartDetected = await DetectDartInImage(new Mat(imagePath));

                if (dartDetected)
                {
                    MessageBox.Show("Dart detected in the uploaded image!", "Detection Result", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No dart detected in the uploaded image.", "Detection Result", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        private void TrackingCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            _enableTracking = true; // Enable tracking when the checkbox is checked.
            _trackingData.Clear(); // Clear the tracking data list.
            _lastTrackingValue = string.Empty; // Reset the last tracking value.
        }

        private void TrackingCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            _enableTracking = false; // Disable tracking when the checkbox is unchecked.
        }
        private async void CaptureVideo()
        {
            MaxCameras = DetectAvailableCameras(); // Detect how many cameras are available.

            var cameraTasks = new Task[MaxCameras];

            while (_isCapturing) // Capture loop
            {
                bool allCamerasReady = true;

                // Launch parallel tasks for each camera
                for (int i = 0; i < MaxCameras; i++)
                {
                    int cameraIndex = i; // Avoid closure issue

                    // Staggered delay to prevent contention between cameras
                    await Task.Delay(cameraIndex * 50); // Adjust delay based on your needs

                    cameraTasks[cameraIndex] = Task.Run(() =>
                    {
                        if (_captures[cameraIndex]?.IsOpened() == true) // If the camera is open
                        {
                            _captures[cameraIndex].Read(_frames[cameraIndex]); // Read frame from camera
                            if (!_frames[cameraIndex].Empty()) // If the frame is not empty
                            {
                                UpdateCameraFeed(cameraIndex); // Update the UI
                            }
                            else
                            {
                                allCamerasReady = false; // Mark the camera as not ready
                            }
                        }
                        else
                        {
                            allCamerasReady = false; // Mark the camera as not ready
                        }
                    });
                }

                // Wait for all camera tasks to complete
                await Task.WhenAll(cameraTasks);

                if (_isComparing && allCamerasReady && !_isSaving) // Ensure comparison only if all cameras are ready
                {
                    await HandleComparisonAndSaveLogic(); // Handle multi-camera image comparison
                }

                await Task.Delay(10); // Small delay to reduce CPU load
            }
        }








        /// <summary>
        /// Detects the number of available cameras.
        /// </summary>
        /// <returns>The number of available cameras.</returns>
        private int DetectAvailableCameras()
        {
            int availableCameras = 0;
            for (int i = 0; i < MaxCameras; i++)
            {
                using (var capture = new VideoCapture(i))
                {
                    if (capture.IsOpened())
                    {
                        availableCameras++;
                    }
                }
            }
            return availableCameras;
        }

        /// <summary>
        /// Updates the camera feed in the UI with the latest frame from the specified camera.
        /// </summary>
        private void UpdateCameraFeed(int cameraIndex)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _bitmaps[cameraIndex] = BitmapSourceConverter.ToBitmapSource(_frames[cameraIndex]);
                    switch (cameraIndex)
                    {
                        case 0:
                            CameraFeed1.Source = _bitmaps[cameraIndex];
                            break;
                        case 1:
                            CameraFeed2.Source = _bitmaps[cameraIndex];
                            break;
                        case 2:
                            CameraFeed3.Source = _bitmaps[cameraIndex];
                            break;
                        case 3:
                            CameraFeed4.Source = _bitmaps[cameraIndex];
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting CameraFeed.Source for camera {cameraIndex}: {ex.Message}");
                }
            });
        }

        private async Task<bool> DetectDartInImage(Mat frame)
        {
            // Save the frame as an image or pass it as a Mat directly
            string tempImagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".jpg");
            frame.SaveImage(tempImagePath);

            // Call the PredictionService to predict if a dart is in the image
            var predictionResult = _predictionService.PredictFromImage(tempImagePath);

            // Check if the result is for the ONNX or TensorFlow model and cast appropriately
            if (predictionResult is OnnxModelOutput onnxOutput)
            {
                // ONNX model: Access Predictions from OnnxModelOutput
                var rslt = onnxOutput.Predictions[0] > 0.5;  // Adjust this logic based on your model's output
                return rslt;

            }
            else if (predictionResult is TensorFlowModelOutput tfOutput)
            {
                // TensorFlow model: Access Predictions from TensorFlowModelOutput
                return tfOutput.Predictions[0] > 0.5;  // Adjust this logic based on your model's output
            }

            throw new InvalidOperationException("Unknown model output type.");
        }


        /// <summary>
        /// Handles comparison logic for all active cameras and checks if the overall average difference
        /// across all cameras exceeds the threshold. Pauses comparison during save and resumes after.
        /// Updates both baseline and calibrated baseline images when conditions are met.
        /// </summary>
        private async Task HandleComparisonAndSaveLogic()
        {
            double totalDifferencePercentage = 0;
            int activeCameraCount = 0;
            bool allCamerasExceedHalfThreshold = true; // Flag to check if all cameras exceed half the threshold
            bool[] cameraReported = new bool[MaxCameras]; // Tracks if each camera has reported a significant difference

            for (int i = 0; i < MaxCameras; i++)
            {
                if (_baselineImages[i] != null && !_frames[i].Empty()) // If the camera is active
                {
                    // Select the appropriate reference image (either baseline or calibrated baseline)
                    Mat referenceImage = _compareToCalibratedBaseline && _calibratedBaselineImages[i] != null
                        ? _calibratedBaselineImages[i]
                        : _baselineImages[i];

                    // Calculate the average percentage of difference over time for this camera
                    double averageDifferencePercentage = await GetAverageDifferencePercentageOverTime(referenceImage, i, 0.5);
                    totalDifferencePercentage += averageDifferencePercentage;
                    activeCameraCount++;

                    // **New Logic: Call the TensorFlow model to detect a dart in the image**
                    //bool dartDetected = await DetectDartInImage(_frames[i]);


                    //if (dartDetected)
                    //{
                    //    Console.WriteLine($"Dart detected in Camera {i + 1}");
                    //    // Optionally, add any further actions you want to take when a dart is detected
                    //}


                    // Update the UI labels with the current difference percentage for this camera
                    UpdateDifferenceLabels(i, averageDifferencePercentage);

                    // Retrieve the threshold for this specific camera
                    double threshold = GetThresholdForCamera(i);

                    // Check if the difference for this camera is greater than half of its threshold
                    if (averageDifferencePercentage > threshold / 2)
                    {
                        cameraReported[i] = true; // Mark this camera as having reported a significant difference
                    }
                    else
                    {
                        allCamerasExceedHalfThreshold = false; // Set the flag to false if any camera doesn't exceed half the threshold
                    }
                }
            }

            // Check if all active cameras have reported in with a significant difference
            bool allCamerasReported = cameraReported.Take(activeCameraCount).All(c => c); // Ensure all active cameras reported

            // Compute the overall average difference across all active cameras
            if (activeCameraCount > 0)
            {
                double overallAverageDifferencePercentage = totalDifferencePercentage / activeCameraCount;

                // Retrieve the global threshold (assuming the same threshold is used for all cameras)
                double globalThreshold = GetGlobalThreshold();

                // Check if all active cameras have reported, exceed half of their individual thresholds,
                // and the overall difference exceeds the global threshold
                if (allCamerasReported && allCamerasExceedHalfThreshold && overallAverageDifferencePercentage > globalThreshold && _allowPromptToSave && _saveCounter < 3)
                {
                    // Ensure that the UI is updated before prompting the user
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Force a UI update to display the latest difference before the prompt
                        for (int i = 0; i < MaxCameras; i++)
                        {
                            UpdateCameraFeed(i); // Update the UI for each camera feed
                        }
                    });

                    // Stop comparison but do not pause video capture or UI updates
                    _isComparing = false;

                    // Prompt the user to save the image and capture a fresh image for the baseline after the prompt
                    await PromptUserToSaveAllCameras(overallAverageDifferencePercentage);

                    // After the user saves the images, update only the baseline images (not calibrated images) for all active cameras
                    for (int i = 0; i < MaxCameras; i++)
                    {
                        if (_baselineImages[i] != null && !_frames[i].Empty()) // If the camera is active
                        {
                            CaptureAndUpdateBaselineImage(i);  // Only updates baseline for each camera
                        }
                    }

                    // Resume comparison logic after the save is complete
                    _isComparing = true;
                }
                else if (overallAverageDifferencePercentage < globalThreshold && _saveCounter >= 3)
                {
                    // Introduce a small delay to stabilize image capture
                    //await Task.Delay(200);

                    // Update both baseline and calibrated baseline images for all cameras
                    UpdateBaselineAndCalibratedImagesForAllCameras();

                    // Reset flags and save counter after updating images
                    _allowPromptToSave = true;
                    _compareToCalibratedBaseline = false;
                    _saveCounter = 0;

                    // Update the UI save counter label
                    Dispatcher.Invoke(() => SaveCounterLabel.Content = $"Save Counter: {_saveCounter}");
                }
                else if (overallAverageDifferencePercentage > 0 && overallAverageDifferencePercentage < globalThreshold)
                {
                    // If the difference is small but growing, update the baseline images for all cameras
                    for (int i = 0; i < MaxCameras; i++)
                    {
                        if (_baselineImages[i] != null && !_frames[i].Empty()) // If the camera is active
                        {
                            CaptureAndUpdateBaselineImage(i); // Update baseline for the active camera
                        }
                    }
                }
            }
        }





        /// <summary>
        /// Retrieves the global threshold value for all cameras.
        /// </summary>
        private double GetGlobalThreshold()
        {
            return Dispatcher.Invoke(() =>
            {
                // You can calculate a global threshold based on multiple sliders or just use the value of one.
                // Here, assuming a global slider for threshold, or you can adjust accordingly.
                return ThresholdSlider1.Value; // Assuming the same threshold for all cameras.
            });
        }



        /// <summary>
        /// Updates both the baseline and calibrated baseline images for all cameras. 
        /// This happens after the difference drops below the threshold.
        /// </summary>
        private void UpdateBaselineAndCalibratedImagesForAllCameras()
        {
            for (int i = 0; i < MaxCameras; i++)
            {
                if (_frames[i] != null && !_frames[i].Empty())
                {
                    // Dispose of old baseline and calibrated baseline images
                    _baselineImages[i]?.Dispose();
                    _calibratedBaselineImages[i]?.Dispose();

                    // Set the current frame as the new baseline and new calibrated baseline image
                    _baselineImages[i] = _frames[i].Clone();
                    _calibratedBaselineImages[i] = _frames[i].Clone();

                    // Save the updated baseline and calibrated baseline images to disk
                    SaveBaselineImage(i);
                    SaveCalibratedBaselineImage(i);

                    // Update the Baseline Image Window to show both baseline and calibrated images
                    Dispatcher.Invoke(() =>
                    {
                        var baselineBitmap = BitmapSourceConverter.ToBitmapSource(_baselineImages[i]);
                        var calibratedBitmap = BitmapSourceConverter.ToBitmapSource(_calibratedBaselineImages[i]);
                        _baselineImageWindow.SetBaselineImages(i, baselineBitmap, calibratedBitmap);
                    });
                }
            }
        }


        /// <summary>
        /// Captures the current frame and updates the baseline image for the specified camera.
        /// Ensures that UI elements are updated on the UI thread using Dispatcher.Invoke.
        /// </summary>
        private void CaptureAndUpdateBaselineImage(int cameraIndex)
        {
            // Ensure this operation happens on the UI thread.
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_frames[cameraIndex] != null && !_frames[cameraIndex].Empty())
                {
                    // Replace the current baseline image with the new frame for the current camera.
                    _baselineImages[cameraIndex]?.Dispose(); // Dispose of the old baseline image.
                    _baselineImages[cameraIndex] = _frames[cameraIndex].Clone(); // Clone the current frame as the new baseline.

                    // Optionally save the new baseline image to disk.
                    SaveBaselineImage(cameraIndex); // Save the updated baseline image for this camera.

                    // Update the Baseline Image Window with the new baseline image.
                    UpdateBaselineWindow(cameraIndex); // Ensure the UI reflects the new baseline image.

                    Console.WriteLine($"Baseline image updated for Camera {cameraIndex} due to growing difference but under threshold.");
                }
            });
        }



        /// <summary>
        /// Updates the UI with the current difference percentage for the specified camera.
        /// </summary>
        private void UpdateDifferenceLabels(int cameraIndex, double differencePercentage)
        {
            Dispatcher.Invoke(() =>
            {
                switch (cameraIndex)
                {
                    case 0:
                        DifferenceLabel1.Content = $"Difference C1: {differencePercentage:F2}%";
                        break;
                    case 1:
                        DifferenceLabel2.Content = $"Difference C2: {differencePercentage:F2}%";
                        break;
                    case 2:
                        DifferenceLabel3.Content = $"Difference C3: {differencePercentage:F2}%";
                        break;
                    case 3:
                        DifferenceLabel4.Content = $"Difference C4: {differencePercentage:F2}%";
                        break;
                }

                // Add the difference to the tracking array if tracking is enabled.
                if (_enableTracking)
                {
                    string newTrackingValue = $"Camera {cameraIndex + 1} Difference: {differencePercentage:F2}%";
                    if (newTrackingValue != _lastTrackingValue) // Skip adding if the value is the same as the last one.
                    {
                        _trackingData.Add(newTrackingValue);
                        _lastTrackingValue = newTrackingValue; // Update the last tracking value.
                    }
                }
            });
        }

        /// <summary>
        /// Calculates the average percentage of difference over a specified duration of time.
        /// </summary>
        private async Task<double> GetAverageDifferencePercentageOverTime(Mat referenceImage, int cameraIndex, double durationInSeconds)
        {
            int frameCount = 0;
            double totalDifferencePercentage = 0.0;
            int numFrames = 0;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start(); // Start timer.

            while (stopwatch.Elapsed.TotalSeconds < durationInSeconds)
            {
                _captures[cameraIndex].Read(_frames[cameraIndex]); // Read frame from camera.
                if (!_frames[cameraIndex].Empty())
                {
                    double differencePercentage = await GetDifferencePercentage(_frames[cameraIndex], referenceImage); // Calculate difference.
                    totalDifferencePercentage += differencePercentage; // Add to total.
                    numFrames++; // Count frames processed.
                }

                await Task.Delay(100); // Delay to avoid CPU overload.
            }

            stopwatch.Stop(); // Stop timer.

            return numFrames > 0 ? totalDifferencePercentage / numFrames : 0.0; // Calculate average.
        }

        /// <summary>
        /// Prompts the user to save the images for all cameras when a significant difference is detected.
        /// </summary>
        private async Task PromptUserToSaveAllCameras(double overallDifferencePercentage)
        {
            Dispatcher.Invoke(() =>
            {
                string message = $"All cameras detected a significant difference of {overallDifferencePercentage:F2}%. Do you want to save the current images?";
                MessageBoxResult result = MessageBox.Show(message, "Save Images", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _saveCounter++;
                    CaptureAndSaveImages(true); // Save images for all cameras.
                }
                else
                {
                    CaptureAndSaveImages(false); // Update baselines without saving images to disk.
                }
            });

            await Task.Delay(100); // Optional: Add a delay to allow the UI to update.
        }

        /// <summary>
        /// Captures images from all cameras and saves them to disk or updates the baseline images.
        /// After the 3rd save, switches to comparing captured images with calibrated baselines.
        /// </summary>
        private readonly SemaphoreSlim _saveSemaphore = new SemaphoreSlim(1, 1);

        private async void CaptureAndSaveImages(bool saveToDisk)
        {
            // Acquire the semaphore asynchronously
            await _saveSemaphore.WaitAsync();

            try
            {
                if (saveToDisk)
                {
                    // Show save dialog to allow the user to enter image name and category
                    var inputDialog = new InputDialog("Enter image name:", "Image Name");

                    // Pre-select the appropriate radio button based on the save counter (_saveCounter)
                    switch (_saveCounter)
                    {
                        case 1:
                            inputDialog.radio1Dart.IsChecked = true;
                            break;
                        case 2:
                            inputDialog.radio2Darts.IsChecked = true;
                            break;
                        case 3:
                            inputDialog.radio3Darts.IsChecked = true;
                            break;
                    }

                    bool? dialogResult = inputDialog.ShowDialog();

                    if (_allowPromptToSave && dialogResult == true) // User clicked Yes/OK
                    {
                        string imageName = inputDialog.ResponseText;
                        string category = inputDialog.SelectedCategory;
                        bool disableBaseline = inputDialog.DisableBaseline;

                        if (string.IsNullOrEmpty(category))
                        {
                            MessageBox.Show("Please select a valid category.", "Invalid Category", MessageBoxButton.OK, MessageBoxImage.Warning);
                            _isSaving = false;
                            return;
                        }

                        string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\CapturedImages");
                        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

                        string categoryPath = Path.Combine(basePath, category);
                        if (!Directory.Exists(categoryPath)) Directory.CreateDirectory(categoryPath);

                        for (int i = 0; i < MaxCameras; i++)
                        {
                            if (_frames[i] != null && !_frames[i].Empty())
                            {

                                // ✅ Apply distortion correction before saving the image
                                Mat correctedFrame = ImageProcessingHelper.CorrectRadialDistortion(_frames[i]);

                                if (i == 0)
                                {
                                    bool dartDetected = await DetectDartInImage(correctedFrame);

                                    if (dartDetected)
                                    {
                                        Console.WriteLine($"Dart detected in Camera {i + 1}");
                                    }
                                }

                                _capturedFrames[i] = correctedFrame.Clone();
                            }
                        }

                        for (int i = 0; i < MaxCameras; i++)
                        {
                            if (_capturedFrames[i] != null)
                            {
                                string imageFileName = $"{imageName}_Camera{i + 1}_{Guid.NewGuid()}.jpg";
                                string imagePath = Path.Combine(categoryPath, imageFileName);
                                _capturedFrames[i].SaveImage(imagePath);

                                if (_saveCounter < 3) // Only update baseline for saves 1 and 2
                                {
                                    _baselineImages[i]?.Dispose();
                                    _baselineImages[i] = _capturedFrames[i].Clone();
                                    UpdateBaselineWindow(i);
                                }
                            }
                        }

                        Dispatcher.Invoke(() => SaveCounterLabel.Content = $"Save Counter: {_saveCounter}");

                        if (_enableTracking)
                        {
                            _trackingData.Add($"Saved Image: {_saveCounter}");
                        }

                        if (_saveCounter >= 3)
                        {
                            _compareToCalibratedBaseline = true;
                            _allowPromptToSave = false;
                        }
                    }
                    else if (dialogResult == false)
                    {
                        for (int i = 0; i < MaxCameras; i++)
                        {
                            if (_capturedFrames[i] != null)
                            {
                                _baselineImages[i]?.Dispose();
                                _baselineImages[i] = _capturedFrames[i].Clone();
                                SaveCalibratedBaselineImage(i);
                                UpdateBaselineWindow(i);
                            }
                        }
                    }
                }

                if (_isComparing && (BaselineCheckbox.IsChecked ?? false))
                {
                    _isComparing = true;
                }
            }
            finally
            {
                // Release the semaphore
                _saveSemaphore.Release();
            }
        }







        /// <summary>
        /// Saves the calibrated baseline image for the specified camera.
        /// </summary>
        private void SaveCalibratedBaselineImage(int cameraIndex)
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\CapturedImages");
            string categoryPath = Path.Combine(basePath, "CalibratedBaseline");
            if (!Directory.Exists(categoryPath)) Directory.CreateDirectory(categoryPath);


            // **Delete previous calibrated baseline images for the camera**
            DeletePreviousImages(cameraIndex, true);

            // Save the new calibrated baseline image
            string imageFileName = $"CalibratedBaseline_Camera{cameraIndex + 1}_{Guid.NewGuid()}.jpg";
            string imagePath = Path.Combine(categoryPath, imageFileName);

            _calibratedBaselineImages[cameraIndex]?.SaveImage(imagePath); // Save calibrated baseline image.
        }

        /// <summary>
        /// Saves the baseline image for the specified camera.
        /// </summary>
        private void SaveBaselineImage(int cameraIndex)
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\CapturedImages");
            string categoryPath = Path.Combine(basePath, "Baseline");
            if (!Directory.Exists(categoryPath)) Directory.CreateDirectory(categoryPath);


            // **Delete previous baseline images for the camera**
            DeletePreviousImages(cameraIndex, false);

            // Save the new baseline image
            string imageFileName = $"Baseline_Camera{cameraIndex + 1}_{Guid.NewGuid()}.jpg";
            string imagePath = Path.Combine(categoryPath, imageFileName);

            _baselineImages[cameraIndex]?.SaveImage(imagePath); // Save baseline image.
        }

        /// <summary>
        /// Event handler triggered when the CameraWindow is closed. Releases resources and cleans up.
        /// </summary>
        private void CameraWindow_Closed(object sender, EventArgs e)
        {
            _isCapturing = false; // Stop capturing.

            // Release and dispose of all camera resources.
            for (int i = 0; i < MaxCameras; i++)
            {
                _captures[i]?.Release();
                _frames[i]?.Dispose();
                _baselineImages[i]?.Dispose();
                _calibratedBaselineImages[i]?.Dispose();
            }

            _baselineImageWindow.Close(); // Close the baseline image window.
            //_differenceWindow.Close(); // Close the difference window.

            // Optionally delete the calibrated baseline images folder.
            string path = Path.Combine("CapturedImages", "CalibratedBaseline");
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true); // Delete the directory and its contents.
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting calibrated baseline folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task<double> GetDifferencePercentage(Mat currentFrame, Mat referenceImage)
        {
            // Ensure referenceImage and currentFrame are valid and not disposed before proceeding.
            if (referenceImage == null || referenceImage.IsDisposed || currentFrame == null || currentFrame.IsDisposed)
                return 0.0;

            // Convert both the current frame and the reference image (baseline) to grayscale.
            // Grayscale makes comparison easier and reduces complexity by working with intensity values only.
            Mat grayCurrent = new Mat();
            Mat grayReference = new Mat();
            Cv2.CvtColor(currentFrame, grayCurrent, ColorConversionCodes.BGR2GRAY); // Convert current frame to grayscale.
            Cv2.CvtColor(referenceImage, grayReference, ColorConversionCodes.BGR2GRAY); // Convert reference (baseline) image to grayscale.

            // Apply a Gaussian blur to both the grayscale images to reduce the effect of minor noise and movements.
            // This helps ignore small pixel changes that might occur due to slight camera movements (wiggle).
            Cv2.GaussianBlur(grayCurrent, grayCurrent, new OpenCvSharp.Size(5, 5), 0); // Blur the current frame.
            Cv2.GaussianBlur(grayReference, grayReference, new OpenCvSharp.Size(5, 5), 0); // Blur the reference (baseline) image.

            // Compute the absolute difference between the two blurred grayscale images.
            Mat diff = new Mat();
            Cv2.Absdiff(grayCurrent, grayReference, diff); // Calculate the pixel-wise absolute difference.

            // Apply a binary threshold to filter out minor intensity differences (below a certain threshold).
            // This helps ignore small differences, focusing only on significant changes.
            Cv2.Threshold(diff, diff, 25, 255, ThresholdTypes.Binary); // Threshold set to 25 to remove minor variations.

            // Count the number of non-zero pixels in the difference image.
            // Non-zero pixels represent areas where significant differences were detected between the two images.
            int nonZeroCount = Cv2.CountNonZero(diff);

            // Calculate the total number of pixels in the image (used for percentage calculation).
            int totalPixels = diff.Rows * diff.Cols;

            // Calculate the percentage of pixels that are different.
            double percentageDifference = (double)nonZeroCount / totalPixels * 100; // Percentage of difference based on non-zero pixels.

            // Return the calculated percentage difference.
            return percentageDifference;
        }


        /// <summary>
        /// Retrieves the threshold value for image comparison for the specified camera.
        /// </summary>
        private double GetThresholdForCamera(int cameraIndex)
        {
            return Dispatcher.Invoke(() =>
            {
                // Retrieve the threshold value from the appropriate slider.
                return cameraIndex switch
                {
                    0 => ThresholdSlider1.Value,
                    //1 => ThresholdSlider2.Value,
                    //2 => ThresholdSlider3.Value,
                    //3 => ThresholdSlider4.Value,
                    _ => 0,
                };
            });
        }

        /// <summary>
        /// Event handler for when the threshold slider value is changed.
        /// Updates the corresponding label in the UI.
        /// </summary>
        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                string sliderName = slider.Name;

                // Update the corresponding threshold label based on the slider's name.
                switch (sliderName)
                {
                    case "ThresholdSlider1":
                        if (ThresholdLabel1 != null)
                        {
                            ThresholdLabel1.Content = $": {slider.Value:F2}";
                        }
                        break;
                        //case "ThresholdSlider2":
                        //    if (ThresholdLabel2 != null)
                        //    {
                        //        ThresholdLabel2.Content = $"Threshold: {slider.Value:F2}";
                        //    }
                        //    break;
                        //case "ThresholdSlider3":
                        //    if (ThresholdLabel3 != null)
                        //    {
                        //        ThresholdLabel3.Content = $"Threshold: {slider.Value:F2}";
                        //    }
                        //    break;
                        //case "ThresholdSlider4":
                        //    if (ThresholdLabel4 != null)
                        //    {
                        //        ThresholdLabel4.Content = $"Threshold: {slider.Value:F2}";
                        //    }
                        break;
                }
            }
        }

        /// <summary>
        /// Handles click events for the camera feeds, expanding the selected feed to full-screen mode.
        /// </summary>
        private void CameraFeed_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image image && image.Source != null)
            {
                int cameraIndex = -1;

                if (image == CameraFeed1)
                {
                    cameraIndex = 0;
                }
                else if (image == CameraFeed2)
                {
                    cameraIndex = 1;
                }
                else if (image == CameraFeed3)
                {
                    cameraIndex = 2;
                }
                else if (image == CameraFeed4)
                {
                    cameraIndex = 3;
                }

                if (cameraIndex >= 0)
                {
                    ExpandedCameraFeed.Source = image.Source;
                    ExpandedCameraFeed.Visibility = Visibility.Visible;
                    CameraGrid.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Handles click events for the expanded camera feed, collapsing it back to the grid view.
        /// </summary>
        private void ExpandedCameraFeed_Click(object sender, MouseButtonEventArgs e)
        {
            ExpandedCameraFeed.Visibility = Visibility.Collapsed;
            CameraGrid.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Event handler for when the baseline checkbox is checked. Starts comparison.
        /// </summary>
        private void BaselineCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < MaxCameras; i++)
            {
                if (_frames[i] != null && !_frames[i].Empty())
                {
                    // Dispose of previous baseline image if it exists
                    _baselineImages[i]?.Dispose();
                    _calibratedBaselineImages[i]?.Dispose(); // Dispose of previous calibrated baseline image if it exists

                    // Capture the current frame as the new baseline image
                    _baselineImages[i] = _frames[i].Clone();
                    _calibratedBaselineImages[i] = _frames[i].Clone();

                    // Save the baseline and calibrated baseline images to separate locations
                    SaveBaselineImage(i);
                    SaveCalibratedBaselineImage(i);


                    // Update the Baseline Image Window with both images
                    UpdateBaselineWindow(i);
                }
            }
            _isComparing = true;
        }

        /// <summary>
        /// Event handler for when the baseline checkbox is unchecked, stopping comparison.
        /// </summary>
        private void BaselineCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            _isComparing = false; // Stop comparing when the baseline checkbox is unchecked.
        }

        /// <summary>
        /// Updates the baseline window with the images for the specified camera.
        /// </summary>
        private void UpdateBaselineWindow(int cameraIndex)
        {
            if (_baselineImages[cameraIndex] != null && _calibratedBaselineImages[cameraIndex] != null)
            {
                var baselineBitmap = BitmapSourceConverter.ToBitmapSource(_baselineImages[cameraIndex]);
                var calibratedBitmap = BitmapSourceConverter.ToBitmapSource(_calibratedBaselineImages[cameraIndex]);
                _baselineImageWindow.SetBaselineImages(cameraIndex, baselineBitmap, calibratedBitmap);
            }
        }

        /// <summary>
        /// Deletes either the baseline or calibrated baseline images for the specified camera.
        /// This ensures that only one type of image (baseline or calibrated) exists for the given camera index.
        /// </summary>
        /// <param name="cameraIndex">The index of the camera.</param>
        /// <param name="deleteCalibrated">If true, delete the calibrated baseline images; otherwise, delete baseline images.</param>
        private void DeletePreviousImages(int cameraIndex, bool deleteCalibrated)
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\CapturedImages");

            if (deleteCalibrated)
            {
                // Define the path for calibrated baseline image folder
                string calibratedBaselinePath = Path.Combine(basePath, "CalibratedBaseline");

                // Define search pattern to locate calibrated baseline images related to the specified camera
                string calibratedBaselinePattern = $"CalibratedBaseline_Camera{cameraIndex + 1}_*.jpg";

                // Delete previous calibrated baseline images for the camera
                if (Directory.Exists(calibratedBaselinePath))
                {
                    var calibratedBaselineFiles = Directory.GetFiles(calibratedBaselinePath, calibratedBaselinePattern);
                    foreach (var file in calibratedBaselineFiles)
                    {
                        File.Delete(file);
                        Console.WriteLine($"Deleted previous calibrated baseline image: {file}");
                    }
                }
            }
            else
            {
                // Define the path for baseline image folder
                string baselinePath = Path.Combine(basePath, "Baseline");

                // Define search pattern to locate baseline images related to the specified camera
                string baselinePattern = $"Baseline_Camera{cameraIndex + 1}_*.jpg";

                // Delete previous baseline images for the camera
                if (Directory.Exists(baselinePath))
                {
                    var baselineFiles = Directory.GetFiles(baselinePath, baselinePattern);
                    foreach (var file in baselineFiles)
                    {
                        File.Delete(file);
                        Console.WriteLine($"Deleted previous baseline image: {file}");
                    }
                }
            }
        }


    }
}
