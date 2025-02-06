using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace DartDetection
{
    public partial class CameraFeedWindow : System.Windows.Window
    {
        private const int IMAGE_WIDTH = 1280;
        private const int IMAGE_HEIGHT = 720;
        private const float DARTBOARD_DIAMETER_MM = 451f;

        // Dartboard radii in mm
        private const float BULLSEYE_RADIUS_MM = 6.35f;
        private const float OUTER_BULL_RADIUS_MM = 15.9f;
        private const float TRIPLE_RING_INNER_RADIUS_MM = 99f;
        private const float TRIPLE_RING_OUTER_RADIUS_MM = 107f;
        private const float DOUBLE_RING_INNER_RADIUS_MM = 162f;
        private const float DOUBLE_RING_OUTER_RADIUS_MM = 170f;

        // Conversion factor: mm → pixels (based on fixed image dimensions)
        private readonly float PIXELS_PER_MM = IMAGE_HEIGHT / DARTBOARD_DIAMETER_MM;


        private List<VideoCapture> cameras = new();
        private int currentCameraIndex = 0;
        private bool isRunning = false;
        private Mat lastFrame;
        private Mat perspectiveMatrix;
        private string matrixFilePath = "perspective_matrix.xml";

        private List<Point2f> selectedPoints = new(); // Stores user clicks

        public CameraFeedWindow()
        {
            InitializeComponent();
            LoadPerspectiveMatrix(); // Load matrix on startup
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[WINDOW] Initial Size: {this.Width} x {this.Height}");
            StartCameras();
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            StopCameras();
        }

        private async void StartCameras()
        {
            if (isRunning) return;
            isRunning = true;
            cameras.Clear();
            CameraImage.Source = null;

            for (int i = 0; i < 5; i++)
            {
                VideoCapture cam = new(i);
                if (!cam.IsOpened())
                {
                    cam.Release();
                    break;
                }

                if (i == 1) // Use the first available camera
                {
                    cam.Set(VideoCaptureProperties.FrameWidth, IMAGE_WIDTH);
                    cam.Set(VideoCaptureProperties.FrameHeight, IMAGE_HEIGHT);
                    cameras.Add(cam);
                }
            }

            if (cameras.Count > 0)
            {
                currentCameraIndex = 0;
                Console.WriteLine($"Using Fixed Resolution: {IMAGE_WIDTH}x{IMAGE_HEIGHT}");
                Console.WriteLine($"PIXELS_PER_MM: {PIXELS_PER_MM}");

                await CaptureFrames();
            }
            else
            {
                MessageBox.Show("No cameras detected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                isRunning = false;
            }
        }


        private async Task CaptureFrames()
        {
            while (isRunning)
            {
                if (cameras.Count > 0 && currentCameraIndex >= 0 && currentCameraIndex < cameras.Count)
                {
                    using Mat frame = new();
                    if (cameras[currentCameraIndex].Read(frame) && !frame.Empty())
                    {
                        lastFrame = frame.Clone();

                        // Apply perspective transformation if matrix exists
                        if (perspectiveMatrix != null && !perspectiveMatrix.Empty())
                        {
                            lastFrame = ApplyPerspectiveTransformation(lastFrame);
                        }

                        BitmapSource bitmap = lastFrame.ToBitmapSource();
                        Dispatcher.Invoke(() =>
                        {
                            CameraImage.Source = bitmap;

                            // 🔹 Log WPF Image Control Size
                            Debug.WriteLine($"[WPF IMAGE] Displayed Size: {CameraImage.ActualWidth} x {CameraImage.ActualHeight}");
                        });
                    }
                }
                await Task.Delay(33);
            }
        }


        private Mat ApplyPerspectiveTransformation(Mat image)
        {
            if (perspectiveMatrix == null || perspectiveMatrix.Empty())
                return image;

            Mat transformedImage = new();
            Cv2.WarpPerspective(image, transformedImage, perspectiveMatrix, new OpenCvSharp.Size(1280, 720));

            Debug.WriteLine($"[TRANSFORMATION] Output Size: {transformedImage.Width} x {transformedImage.Height}");

            return transformedImage;
        }






        private void LoadPerspectiveMatrix()
        {
            if (File.Exists(matrixFilePath))
            {
                using (FileStorage fs = new FileStorage(matrixFilePath, FileStorage.Modes.Read))
                {
                    perspectiveMatrix = new Mat();
                    fs["perspective_matrix"].ReadMat(perspectiveMatrix);
                }
            }
        }

        private void SavePerspectiveMatrix()
        {
            if (perspectiveMatrix == null)
                return;

            using (FileStorage fs = new FileStorage(matrixFilePath, FileStorage.Modes.Write))
            {
                fs.Write("perspective_matrix", perspectiveMatrix);
            }
        }

        private void Calibrate_Click(object sender, RoutedEventArgs e)
        {
            if (lastFrame == null)
            {
                MessageBox.Show("No frame available for calibration. Start the camera first.", "Error");
                return;
            }

            // 🔹 Reset the perspective matrix
            if (perspectiveMatrix != null)
            {
                perspectiveMatrix.Dispose();
                perspectiveMatrix = null;
            }

            selectedPoints.Clear();

            // 🔹 Delete existing calibration file
            if (File.Exists(matrixFilePath))
            {
                File.Delete(matrixFilePath);
                Console.WriteLine("Old perspective matrix deleted.");
            }

            MessageBox.Show("Click 4 points on the dartboard in a clockwise order.", "Calibration");
        }


        private void CameraImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (selectedPoints.Count < 4)
            {
                System.Windows.Point mousePosition = e.GetPosition(CameraImage);

                // Correct scaling by getting actual displayed dimensions
                double displayedWidth = CameraImage.ActualWidth;
                double displayedHeight = CameraImage.ActualHeight;

                double scaleX = IMAGE_WIDTH / displayedWidth;
                double scaleY = IMAGE_HEIGHT / displayedHeight;

                float correctedX = (float)(mousePosition.X * scaleX);
                float correctedY = (float)(mousePosition.Y * scaleY);

                selectedPoints.Add(new Point2f(correctedX, correctedY));

                Console.WriteLine($"Clicked: WPF({mousePosition.X}, {mousePosition.Y}) → OpenCV({correctedX}, {correctedY})");

                if (selectedPoints.Count == 4)
                {
                    ComputePerspectiveMatrix();
                    LoadPerspectiveMatrix();
                }
            }
        }






        private void ComputePerspectiveMatrix()
        {
            if (selectedPoints.Count != 4)
            {
                MessageBox.Show("Please select exactly 4 points.", "Error");
                return;
            }

            // 🔹 Define the dartboard reference points in OpenCV coordinates
            Point2f[] boardPoints = {
        new Point2f(0, 0),                           // Top-left
        new Point2f(IMAGE_WIDTH - 1, 0),             // Top-right
        new Point2f(IMAGE_WIDTH - 1, IMAGE_HEIGHT - 1), // Bottom-right
        new Point2f(0, IMAGE_HEIGHT - 1)             // Bottom-left
    };

            perspectiveMatrix = Cv2.GetPerspectiveTransform(selectedPoints.ToArray(), boardPoints);
            SavePerspectiveMatrix();
            MessageBox.Show("Calibration completed!", "Success");
        }


        private void ResetCalibration_Click(object sender, RoutedEventArgs e)
        {
            perspectiveMatrix = null;
            File.Delete(matrixFilePath);
            MessageBox.Show("Calibration reset!", "Info");
        }

        private void StopCameras()
        {
            isRunning = false;
            foreach (var cam in cameras) cam.Release();
            cameras.Clear();
            Dispatcher.Invoke(() => CameraImage.Source = null);
        }

        private void StartCameras_Click(object sender, RoutedEventArgs e)
        {
            StartCameras();
        }

        private void StopCameras_Click(object sender, RoutedEventArgs e)
        {
            StopCameras();
        }

        private void PreviousCamera_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning && cameras.Count > 1)
            {
                currentCameraIndex = (currentCameraIndex - 1 + cameras.Count) % cameras.Count;
            }
        }

        private void NextCamera_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning && cameras.Count > 1)
            {
                currentCameraIndex = (currentCameraIndex + 1) % cameras.Count;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            StopCameras();
            this.Close();
        }
    }
}
