using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace DartDetection
{
    public partial class DartboardDetectionWindow : System.Windows.Window
    {
        private OpenCvSharp.Mat originalMat; // Original image
        private OpenCvSharp.Mat processedMat; // Processed image

        public DartboardDetectionWindow()
        {
            InitializeComponent();
        }

        // Event handler for selecting an image
        private void SelectImageButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                originalMat = OpenCvSharp.Cv2.ImRead(openFileDialog.FileName, OpenCvSharp.ImreadModes.Color);

                if (originalMat.Empty())
                {
                    System.Windows.MessageBox.Show("Failed to load the image. Please try another file.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                // Display the original image
                DartboardImage.Source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(originalMat);
                processedMat = originalMat.Clone();
            }
        }

        // Event handler for detecting the dartboard
        private void ProcessImageButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (originalMat == null || originalMat.Empty())
            {
                System.Windows.MessageBox.Show("Please select an image first.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            DetectDartboard(processedMat.Clone());
        }

        // Main dartboard detection logic
        private void DetectDartboard(OpenCvSharp.Mat inputImage)
        {
            try
            {
                // Preprocess the image
                OpenCvSharp.Mat grayImage = new OpenCvSharp.Mat();
                Cv2.CvtColor(inputImage, grayImage, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
                Cv2.GaussianBlur(grayImage, grayImage, new OpenCvSharp.Size(9, 9), 2);

                // Optional: Enhance contrast
                Cv2.EqualizeHist(grayImage, grayImage);

                // Detect circles
                CircleSegment[] circles = Cv2.HoughCircles(
                    grayImage,
                    HoughModes.Gradient,
                    dp: 1.2,
                    minDist: grayImage.Height / 8,
                    param1: 150, // Adjusted Canny threshold
                    param2: 20,  // Adjusted accumulator threshold
                    minRadius: 300, // Adjust for dartboard size
                    maxRadius: 500
                );

                if (circles != null && circles.Length > 0)
                {
                    // Validate and select the largest circle
                    CircleSegment dartboardCircle = circles.OrderByDescending(c => c.Radius).First();

                    // Optional: Validate that the circle is roughly centered
                    double imageCenterX = inputImage.Width / 2.0;
                    double imageCenterY = inputImage.Height / 2.0;
                    if (Math.Abs(dartboardCircle.Center.X - imageCenterX) > 100 ||
                        Math.Abs(dartboardCircle.Center.Y - imageCenterY) > 100)
                    {
                        System.Windows.MessageBox.Show("Dartboard not detected at the expected location.", "Detection Failed");
                        return;
                    }

                    // Draw the detected circle
                    Cv2.Circle(inputImage,
                               (OpenCvSharp.Point)dartboardCircle.Center,
                               (int)dartboardCircle.Radius,
                               OpenCvSharp.Scalar.Red,
                               2);

                    // Set the center for radial lines
                    OpenCvSharp.Point dartboardCenter = (OpenCvSharp.Point)dartboardCircle.Center;

                    Dispatcher.Invoke(() =>
                    {
                        DartboardImage.Source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(inputImage);
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show("No dartboard detected. Try adjusting parameters.", "Detection Failed");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error during dartboard detection: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }


        // Event handler for resetting the image
        private void ResetButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (originalMat != null)
            {
                DartboardImage.Source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(originalMat);
                processedMat = originalMat.Clone();
            }
        }

        // Event handler for slider value changes (optional dynamic updates)
        private void Slider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (processedMat == null || processedMat.Empty())
                return;

            // Re-run detection with updated slider values
            DetectDartboard(processedMat.Clone());
        }
    }
}
