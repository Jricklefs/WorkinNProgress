﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DartDetection.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace DartDetection
{
    public partial class DartboardConfigPage : System.Windows.Window
    {


        // The source image (e.g. dartboard) loaded via OpenCV.
        private Mat dartboardMat;
        // The destination reference image (Polar_Graph_Paper) loaded via OpenCV.
        private Mat polarOriginalMat;
        // Zoom factor and base size for the polar image.
        private double polarZoom;
        private  int polarBaseSize;
        // Lists to store four points for each mode.
        private readonly List<Point2f> polarReferencePoints = new List<Point2f>();
        private readonly List<Point2f> sourcePoints = new List<Point2f>();
        private readonly AppSettings _appSettings;
        private OpenCvSharp.Mat resizedDartboard; // Store the resized dartboard globally
        private int centerOffsetX = 0; // X-axis fine-tune adjustment
        private int centerOffsetY = 0; // Y-axis fine-tune adjustment
        private const int OffsetStep = 1; // Step size for adjustments


        // Flag to indicate which image is currently visible.
        // true = Polar Graph image is visible (capture destination/reference points).
        // false = Dartboard (source) image is visible (capture source points).
        private bool isPolarGraphVisible = false;

        public DartboardConfigPage()
        {
            InitializeComponent();
            _appSettings = SettingsManager.LoadSettings();

            polarZoom = _appSettings.PolarGraphDefaultZoom;
            polarBaseSize = _appSettings.PolarGraphBaseSize;

            // Subscribe to mouse clicks on the canvas.
            ImageCanvas.MouseLeftButtonDown += ImageCanvas_MouseLeftButtonDown;
        }

        // Button click handlers for adjusting offsets
        private void AdjustOffsetUp(object sender, RoutedEventArgs e)
        {
            centerOffsetY -= OffsetStep;
            RedrawDartboard();
        }

        private void AdjustOffsetDown(object sender, RoutedEventArgs e)
        {
            centerOffsetY += OffsetStep;
            RedrawDartboard();
        }

        private void AdjustOffsetLeft(object sender, RoutedEventArgs e)
        {
            centerOffsetX -= OffsetStep;
            RedrawDartboard();
        }

        private void AdjustOffsetRight(object sender, RoutedEventArgs e)
        {
            centerOffsetX += OffsetStep;
            RedrawDartboard();
        }

        private void ResetOffsets(object sender, RoutedEventArgs e)
        {
            centerOffsetX = 0;
            centerOffsetY = 0;
            RedrawDartboard();
        }
        // Redraw the dartboard with updated offsets
        private void RedrawDartboard()
        {
            if (resizedDartboard == null || resizedDartboard.Empty())
            {
                System.Windows.MessageBox.Show("No transformed dartboard image available. Transform the image first.",
                                               "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            OpenCvSharp.Mat dartboardWithGrid = DrawScoringSystem(resizedDartboard);
            DartboardImage.Source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(dartboardWithGrid);
        }

        // Settings button loads the Polar Graph image.
        private void PolarGraphButton_Click(object sender, RoutedEventArgs e)
        {
            // Load the polar image if not already loaded.
            if (polarOriginalMat == null)
            {
                string imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _appSettings.PolarGraphImagePath);
                // Load with transparency preserved.
                polarOriginalMat = Cv2.ImRead(imagePath, ImreadModes.Unchanged);
                if (polarOriginalMat == null || polarOriginalMat.Empty())
                {
                    MessageBox.Show($"Failed to load {imagePath}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                // Optionally composite on white.
                polarOriginalMat = CompositeOnWhite(polarOriginalMat);
            }

            // Use the default zoom and base size from settings.
            polarZoom = _appSettings.PolarGraphDefaultZoom;
            polarBaseSize = _appSettings.PolarGraphBaseSize;
            UpdatePolarImage();

            // Clear any existing reference points in memory.
            polarReferencePoints.Clear();
            // Clear any markers on the canvas.
            ClearMarkers();

            // Check if there are stored Polar Graph points in settings.
            if (_appSettings.PolarGraphPoints != null && _appSettings.PolarGraphPoints.Count == 4)
            {
                // Convert the stored points to OpenCvSharp.Point2f and update our local list.
                foreach (var pt in _appSettings.PolarGraphPoints)
                {
                    Point2f cvPt = new Point2f(pt.X, pt.Y);
                    polarReferencePoints.Add(cvPt);
                    // Draw a blue marker on the canvas for each point.
                    DrawMarker(new System.Windows.Point(cvPt.X, cvPt.Y), Colors.Blue);
                }
                MessageBox.Show("Loaded 4 stored Polar Graph points.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No stored Polar Graph points found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Set mode so that any new clicks capture destination points.
            isPolarGraphVisible = true;

            // Optionally, disable the Save button until changes are made.
            SaveSettingsButton.IsEnabled = false;
        }

        private void ClearMarkers()
        {
            // Assuming the canvas only contains markers along with your Image control,
            // you can clear the children and then re-add the Image control if needed.
            ImageCanvas.Children.Clear();
            // Optionally, add back the Image control if it was removed.
            ImageCanvas.Children.Add(DartboardImage);
        }



        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPolarGraphVisible)
            {
                // Validate that exactly 4 polar points are captured.
                if (polarReferencePoints.Count != 4)
                {
                    MessageBox.Show("You must capture exactly 4 points for the Polar Graph before saving.",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Convert the captured polar points to your settings model (PointData).
                var polarPoints = polarReferencePoints.Select(p => new PointData { X = p.X, Y = p.Y }).ToList();

                // Update your settings.
                _appSettings.PolarGraphPoints = polarPoints;

                // Save the settings.
                SettingsManager.SaveSettings(_appSettings);

                MessageBox.Show("Polar Graph settings saved successfully.",
                                "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Validate that exactly 4 source points are captured.
                if (sourcePoints.Count != 4)
                {
                    MessageBox.Show("You must capture exactly 4 points for the camera image before saving.",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Convert the captured source points to your settings model.
                var cameraPoints = sourcePoints.Select(p => new PointData { X = p.X, Y = p.Y }).ToList();

                // Check if a camera with the name "Camera1" already exists.
                var camera = _appSettings.Cameras.FirstOrDefault(c => c.CameraName == "Camera1");
                if (camera == null)
                {
                    // Create a new camera setting.
                    camera = new CameraSettings { CameraName = "Camera1", TransformationPoints = cameraPoints };
                    _appSettings.Cameras.Add(camera);
                }
                else
                {
                    // Update existing camera settings.
                    camera.TransformationPoints = cameraPoints;
                }

                // Save the settings.
                SettingsManager.SaveSettings(_appSettings);

                MessageBox.Show("Camera1 settings saved successfully.",
                                "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Optionally, disable the Save button until further changes occur.
            SaveSettingsButton.IsEnabled = false;
        }



        // Upload Image button loads the dartboard (source) image.
        private void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Load the source (camera) image.
                dartboardMat = Cv2.ImRead(openFileDialog.FileName, ImreadModes.Color);
                if (dartboardMat == null || dartboardMat.Empty())
                {
                    MessageBox.Show("Failed to load the selected image.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Display the loaded image.
                DartboardImage.Source = BitmapSourceConverter.ToBitmapSource(dartboardMat);

                // Clear any previously captured source points and markers.
                sourcePoints.Clear();
                ClearMarkers();

                // Set mode to source image mode.
                isPolarGraphVisible = false;

                // Check if settings contain saved points for Camera1.
                var camera = _appSettings.Cameras.FirstOrDefault(c => c.CameraName == "Camera1");
                if (camera != null && camera.TransformationPoints != null && camera.TransformationPoints.Count == 4)
                {
                    // Convert each saved point (PointData) into an OpenCvSharp Point2f,
                    // add them to the sourcePoints list, and draw markers.
                    foreach (var pt in camera.TransformationPoints)
                    {
                        Point2f cvPt = new Point2f(pt.X, pt.Y);
                        sourcePoints.Add(cvPt);
                        // Draw a marker with red color for source points.
                        DrawMarker(new System.Windows.Point(cvPt.X, cvPt.Y), Colors.Red);
                    }
                    MessageBox.Show("Loaded 4 saved source points for Camera1.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Enable the Save button if needed.
                    SaveSettingsButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("No saved source points for Camera1 found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Optionally disable the Save button until 4 new points are captured.
                    SaveSettingsButton.IsEnabled = false;
                }
            }
        }
        private void ClearPointsButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear local point collections.
            polarReferencePoints.Clear();
            sourcePoints.Clear();

            // Clear any markers from the canvas.
            ClearMarkers();

            // Optionally, if you want to clear the saved settings for the points as well,
            // you can update the settings and then save them:
            _appSettings.PolarGraphPoints.Clear();

            // For Camera1: find and clear transformation points if they exist.
            var camera = _appSettings.Cameras.FirstOrDefault(c => c.CameraName == "Camera1");
            if (camera != null)
            {
                camera.TransformationPoints.Clear();
            }
            SettingsManager.SaveSettings(_appSettings);

            // Optionally disable the Save button if needed.
            SaveSettingsButton.IsEnabled = false;

            MessageBox.Show("All Polar Graph and Camera points have been cleared.",
                            "Points Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        // Transform Image button applies the perspective transformation.
        private void TransformButton_Click(object sender, RoutedEventArgs e)
        {
            ClearMarkers();

            if (sourcePoints.Count != 4 || polarReferencePoints.Count != 4)
            {
                MessageBox.Show("Both images need 4 captured points. Capture points on the Polar Graph image and the source image first.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Compute the perspective transformation from source points to polar reference points.
            Mat transformMatrix = Cv2.GetPerspectiveTransform(sourcePoints.ToArray(), polarReferencePoints.ToArray());

            // Define an output size (here, using BaseSize for both dimensions).
            int outputWidth = polarBaseSize;
            int outputHeight = polarBaseSize;

            Mat warpedImage = new Mat();
            Cv2.WarpPerspective(dartboardMat, warpedImage, transformMatrix, new OpenCvSharp.Size(outputWidth, outputHeight));

            // Detect the outer perimeter
            RotatedRect outerPerimeter = new RotatedRect();
            try
            {
                 outerPerimeter = DetectOuterPerimeter(warpedImage);

                // Highlight the detected perimeter
                Cv2.Ellipse(warpedImage, outerPerimeter, Scalar.BlueViolet, 1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Outer perimeter detection failed: {ex.Message}", "Detection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            // Crop the image to the detected outer perimeter
            Mat croppedDartboard = CropToOuterPerimeter(warpedImage, outerPerimeter);

            // Resize to match Polar Graph size
            resizedDartboard = new Mat();
            Cv2.Resize(croppedDartboard, resizedDartboard, new OpenCvSharp.Size(polarBaseSize, polarBaseSize));

            RedrawDartboard();
            OpenCvSharp.Mat dartboardWithCoordinates = DrawScoringSystem(resizedDartboard);
           
            DartboardImage.Source = BitmapSourceConverter.ToBitmapSource(dartboardWithCoordinates);

            MessageBox.Show("Perspective transformation applied.");
        }
        private OpenCvSharp.Mat DrawScoringSystem(OpenCvSharp.Mat dartboard)
        {
            OpenCvSharp.Mat overlay = dartboard.Clone();

            // Apply fine-tuning offsets
            int centerX = (overlay.Width / 2) + centerOffsetX;
            int centerY = (overlay.Height / 2) + centerOffsetY;
            int radius = overlay.Width / 2;

            // Correct dartboard wedge numbering (starting from 12 o'clock at -90°)
            int[] dartboardNumbers = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17,
                               3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

            for (int i = 0; i < 20; i++)
            {
                double startAngle = -99 + (i * 18); // Adjusted starting angle (-99°)
                double endAngle = startAngle + 18;

                double startRadians = startAngle * System.Math.PI / 180.0;
                double endRadians = endAngle * System.Math.PI / 180.0;

                // Compute wedge boundary points with offsets
                int x1 = centerX + (int)(radius * System.Math.Cos(startRadians));
                int y1 = centerY + (int)(radius * System.Math.Sin(startRadians));
                int x2 = centerX + (int)(radius * System.Math.Cos(endRadians));
                int y2 = centerY + (int)(radius * System.Math.Sin(endRadians));

                // Draw wedge boundaries
                OpenCvSharp.Cv2.Line(overlay, new OpenCvSharp.Point(centerX, centerY), new OpenCvSharp.Point(x1, y1), OpenCvSharp.Scalar.Yellow, 1);
                OpenCvSharp.Cv2.Line(overlay, new OpenCvSharp.Point(centerX, centerY), new OpenCvSharp.Point(x2, y2), OpenCvSharp.Scalar.Yellow, 1);

                // Place numbers slightly inside the double ring (~75% of the radius) with offsets
                int textX = centerX + (int)((radius * 0.75) * System.Math.Cos((startRadians + endRadians) / 2));
                int textY = centerY + (int)((radius * 0.75) * System.Math.Sin((startRadians + endRadians) / 2));

                OpenCvSharp.Cv2.PutText(overlay, dartboardNumbers[i].ToString(),
                                        new OpenCvSharp.Point(textX, textY),
                                        OpenCvSharp.HersheyFonts.HersheySimplex, 0.6, OpenCvSharp.Scalar.White, 1);
            }

            return overlay;
        }






        private (string Area, int Multiplier) CalculateScoringArea(OpenCvSharp.Point dartPosition, int boardSize)
        {
            int centerX = boardSize / 2;
            int centerY = boardSize / 2;

            double dx = dartPosition.X - centerX;
            double dy = dartPosition.Y - centerY;
            double distance = System.Math.Sqrt(dx * dx + dy * dy);

            // Standard dartboard rings relative to board size
            double bullseyeRadius = boardSize * 0.05;
            double outerBullRadius = boardSize * 0.10;
            double tripleRingInner = boardSize * 0.38;
            double tripleRingOuter = boardSize * 0.41;
            double doubleRingInner = boardSize * 0.63;
            double doubleRingOuter = boardSize * 0.66;

            // Determine the scoring area
            if (distance < bullseyeRadius) return ("Bullseye", 1);
            if (distance < outerBullRadius) return ("Outer Bull", 1);
            if (distance > tripleRingInner && distance < tripleRingOuter) return ("Triple", 3);
            if (distance > doubleRingInner && distance < doubleRingOuter) return ("Double", 2);

            return ("Single", 1);
        }


        private Mat CropToOuterPerimeter(Mat image, RotatedRect outerPerimeter)
        {
            // Define bounding rectangle around the detected perimeter
            OpenCvSharp.Rect boundingBox = outerPerimeter.BoundingRect();

            // Ensure ROI is within image bounds
            boundingBox.X = Math.Max(0, boundingBox.X);
            boundingBox.Y = Math.Max(0, boundingBox.Y);
            boundingBox.Width = Math.Min(image.Width - boundingBox.X, boundingBox.Width);
            boundingBox.Height = Math.Min(image.Height - boundingBox.Y, boundingBox.Height);

            // Crop the dartboard using the bounding rectangle
            return new Mat(image, boundingBox);
        }


        // Detect the outer perimeter of the dartboard automatically
        private OpenCvSharp.RotatedRect DetectOuterPerimeter(OpenCvSharp.Mat image)
        {
            var gray = new OpenCvSharp.Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            // Get edges using adaptive thresholding
            var edges = GetEdges(gray);

            // Debug: Save edges for visualization
            edges.SaveImage("edges_debug.png");

            OpenCvSharp.Point[][] contours;
            OpenCvSharp.HierarchyIndex[] hierarchy;
            Cv2.FindContours(edges, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // Debug: Save contours for visualization
            var contourImage = image.Clone();
            Cv2.DrawContours(contourImage, contours, -1, Scalar.Green, 2);
            contourImage.SaveImage("contours_debug.png");

            // Filter valid contours based on area and circularity
            var validContours = contours.Where(c =>
            {
                double area = Cv2.ContourArea(c);
                if (area < 1000 || area > image.Width * image.Height * 0.9)
                    return false;

                var approxCurve = c.Select(p => new OpenCvSharp.Point2f(p.X, p.Y)).ToArray();
                double perimeter = Cv2.ArcLength(approxCurve, true);
                double circularity = (4 * Math.PI * area) / (perimeter * perimeter);
                return circularity > 0.5; // Relaxed circularity threshold
            });

            var largestContour = validContours.OrderByDescending(c => Cv2.ContourArea(c)).FirstOrDefault();
            if (largestContour == null || largestContour.Length < 5)
            {
                largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).FirstOrDefault();
            }

            if (largestContour == null || largestContour.Length < 5)
            {
                throw new InvalidOperationException("Failed to detect the outer perimeter.");
            }

            return Cv2.FitEllipse(largestContour);
        }

        // Detect edges using adaptive thresholding
        private OpenCvSharp.Mat GetEdges(OpenCvSharp.Mat grayImage)
        {
            var filtered = new OpenCvSharp.Mat();
            Cv2.BilateralFilter(grayImage, filtered, 9, 75, 75); // Reduce noise while preserving edges

            // Adaptive thresholding
            var thresholded = new OpenCvSharp.Mat();
            Cv2.AdaptiveThreshold(filtered, thresholded, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 11, 2);

            return thresholded;
        }



        // Mouse click on the canvas to capture points.
        private void ImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point clickPosition = e.GetPosition(ImageCanvas);
            Point2f cvPoint = new Point2f((float)clickPosition.X, (float)clickPosition.Y);

            if (isPolarGraphVisible)
            {
                polarReferencePoints.Add(cvPoint);
                DrawMarker(clickPosition, Colors.Blue); // Blue markers for polar reference points.

                if (polarReferencePoints.Count == 4)
                {
                    SaveSettingsButton.IsEnabled = true;
                    MessageBox.Show("Captured 4 reference points on the Polar Graph image.",
                                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                sourcePoints.Add(cvPoint);
                DrawMarker(clickPosition, Colors.Red); // Red markers for source points.

                if (sourcePoints.Count == 4)
                {
                    SaveSettingsButton.IsEnabled = true;
                    MessageBox.Show("Captured 4 source points on the dartboard image. These will be saved as Camera1.",
                                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }



        // Helper method to draw a marker at the specified position with the given color.
        private void DrawMarker(System.Windows.Point position, Color color)
        {
            var marker = new System.Windows.Shapes.Ellipse
            {
                Fill = new SolidColorBrush(color),
                Width = 10,
                Height = 10
            };
            System.Windows.Controls.Canvas.SetLeft(marker, position.X - marker.Width / 2);
            System.Windows.Controls.Canvas.SetTop(marker, position.Y - marker.Height / 2);
            ImageCanvas.Children.Add(marker);
        }

        // Update the displayed Polar Graph image with the current zoom factor.
        private void UpdatePolarImage()
        {
            int width = (int)(polarBaseSize * polarZoom);
            int height = (int)(polarBaseSize * polarZoom);
            Mat resizedPolar = new Mat();
            Cv2.Resize(polarOriginalMat, resizedPolar, new OpenCvSharp.Size(width, height));
            DartboardImage.Source = BitmapSourceConverter.ToBitmapSource(resizedPolar);
        }

        // Composite a BGRA image onto a white background.
        private Mat CompositeOnWhite(Mat img)
        {
            if (img.Channels() != 4)
                return img;

            Mat whiteBg = new Mat(img.Size(), MatType.CV_8UC3, Scalar.White);
            Mat[] channels = Cv2.Split(img);
            Mat b = channels[0], g = channels[1], r = channels[2], a = channels[3];

            Mat b_f = new Mat(), g_f = new Mat(), r_f = new Mat(), a_f = new Mat();
            b.ConvertTo(b_f, MatType.CV_32F);
            g.ConvertTo(g_f, MatType.CV_32F);
            r.ConvertTo(r_f, MatType.CV_32F);
            a.ConvertTo(a_f, MatType.CV_32F, 1.0 / 255.0);

            Mat bgr = new Mat();
            Cv2.Merge(new Mat[] { b_f, g_f, r_f }, bgr);
            Mat alpha3 = new Mat();
            Cv2.Merge(new Mat[] { a_f, a_f, a_f }, alpha3);
            Mat white_f = new Mat();
            whiteBg.ConvertTo(white_f, MatType.CV_32FC3);

            Mat foreground = new Mat(), background = new Mat();
            Cv2.Multiply(bgr, alpha3, foreground);
            Cv2.Multiply(white_f, Scalar.All(1.0) - alpha3, background);
            Mat result_f = new Mat();
            Cv2.Add(foreground, background, result_f);
            Mat result = new Mat();
            result_f.ConvertTo(result, MatType.CV_8UC3);
            return result;
        }

        // Prevent the window from maximizing when double-clicking the Settings button.
        private void SettingsButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void SettingsButton_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
