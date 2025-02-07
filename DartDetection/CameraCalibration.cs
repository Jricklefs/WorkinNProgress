using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using DartDetection;

namespace DartDetection
{
    public class CameraCalibrator : IDisposable
    {
        public Mat CameraMatrix { get; private set; }
        public Mat DistCoeffs { get; private set; }
        public double ReprojectionError { get; private set; }

        private readonly AppSettings _appSettings;

        public CameraCalibrator()
        {
            CameraMatrix = new Mat();
            DistCoeffs = new Mat();
            _appSettings = SettingsManager.LoadSettings();
        }

        /// <summary>
        /// Runs camera calibration and saves results to settings.
        /// </summary>
        public bool CalibrateCamera(string imageDirectory, Size patternSize)
        {
            try
            {
                string[] imageFiles = Directory.GetFiles(imageDirectory, "*.jpg");

                if (imageFiles.Length < 5)
                {
                    Console.WriteLine("Not enough images for calibration. At least 5 images required.");
                    return false;
                }

                List<List<Point3f>> objectPoints = new List<List<Point3f>>();
                List<List<Point2f>> imagePoints = new List<List<Point2f>>();
                List<Point3f> objectPointsTemplate = CreateObjectPoints(patternSize);

                Size imageSize = new Size();

                foreach (var file in imageFiles)
                {
                    Mat image = Cv2.ImRead(file, ImreadModes.Grayscale);

                    // ✅ Ensure the image is valid
                    if (image == null || image.Empty())
                    {
                        Console.WriteLine($"Skipping {file}: Failed to load.");
                        continue;
                    }

                    if (imageSize.Width == 0 || imageSize.Height == 0)
                        imageSize = new Size(image.Width, image.Height);


         
    

                    Point2f[] corners;
                    bool found = Cv2.FindChessboardCorners(image, patternSize, out corners);

                    if (found)
                    {
                        objectPoints.Add(new List<Point3f>(objectPointsTemplate));
                        imagePoints.Add(corners.ToList());

                        Cv2.DrawChessboardCorners(image, patternSize, corners, found);

                    }

                    image.Dispose();  // ✅ Prevent memory leaks
                }

                // ✅ Ensure we have enough points before calibration
                if (objectPoints.Count < 5 || imagePoints.Count < 5)
                {
                    Console.WriteLine("Not enough valid images for calibration.");
                    return false;
                }

                Mat cameraMatrix = new Mat();
                Mat distCoeffs = new Mat();
                Mat[] rvecs, tvecs;

                List<Mat> objectPointsMat = ConvertToMatList(objectPoints, MatType.CV_32FC3);
                List<Mat> imagePointsMat = ConvertToMatList(imagePoints, MatType.CV_32FC2);

                // Perform camera calibration
                ReprojectionError = Cv2.CalibrateCamera(
                    objectPointsMat, imagePointsMat, imageSize,
                    cameraMatrix, distCoeffs, out rvecs, out tvecs
                );

                Console.WriteLine("Calibration Completed");
                Console.WriteLine("Camera Matrix:\n" + cameraMatrix.Dump());
                Console.WriteLine("Distortion Coefficients:\n" + distCoeffs.Dump());

                // ✅ Save calibration immediately after calibration completes
                SaveCalibrationToSettings(cameraMatrix, distCoeffs);

                // ✅ Free memory for temporary Mats
                cameraMatrix.Dispose();
                distCoeffs.Dispose();
                foreach (var mat in objectPointsMat) mat.Dispose();
                foreach (var mat in imagePointsMat) mat.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during calibration: " + ex.Message);
                return false;
            }
        }

        private Mat OverlayCheckerboard(Mat image, Size patternSize, int squareSize = 50)
        {
            Mat overlay = image.Clone();
            int rows = patternSize.Height;
            int cols = patternSize.Width;

            // Determine the center position for the checkerboard
            int centerX = image.Width / 2 - (cols * squareSize) / 2;
            int centerY = image.Height / 2 - (rows * squareSize) / 2;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    Scalar color = (i + j) % 2 == 0 ? Scalar.White : Scalar.Black;

                    // Calculate the square position relative to the dartboard
                    int x = centerX + j * squareSize;
                    int y = centerY + i * squareSize;

                    // Ensure the checkerboard does not go out of bounds
                    if (x + squareSize > image.Width || y + squareSize > image.Height)
                        continue;

                    Rect rect = new Rect(x, y, squareSize, squareSize);
                    Cv2.Rectangle(overlay, rect, color, -1);
                }
            }

            return overlay;
        }



        private void SaveCalibrationToSettings(Mat cameraMatrix, Mat distCoeffs)
{
    if (cameraMatrix.Empty() || distCoeffs.Empty())
    {
        Console.WriteLine("Calibration data is empty. Not saving.");
        return;
    }

    // ✅ Convert CameraMatrix to a jagged array
    _appSettings.CameraMatrix = ConvertMatToJaggedArray(cameraMatrix);

    // ✅ Convert DistCoeffs to a flat array
    _appSettings.DistCoeffs = ConvertMatToArray(distCoeffs);

    try
    {
        SettingsManager.SaveSettings(_appSettings);
        Console.WriteLine("Calibration settings saved successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error saving calibration settings: " + ex.Message);
    }
}


        /// <summary>
        /// Converts List<List<PointT>> to List<Mat>
        /// </summary>
        private List<Mat> ConvertToMatList<T>(List<List<T>> points, MatType type) where T : struct
        {
            return points.Select(list =>
            {
                Mat mat = new Mat(list.Count, 1, type);
                for (int i = 0; i < list.Count; i++)
                    mat.Set(i, 0, list[i]);
                return mat;
            }).ToList();
        }

        private double[][] ConvertMatToJaggedArray(Mat mat)
        {
            double[][] matArray = new double[mat.Rows][];
            double[] tempArray = new double[mat.Rows * mat.Cols];

            mat.GetArray(out tempArray); // ✅ Extract data into a 1D array

            for (int i = 0; i < mat.Rows; i++)
            {
                matArray[i] = new double[mat.Cols];
                for (int j = 0; j < mat.Cols; j++)
                {
                    matArray[i][j] = tempArray[i * mat.Cols + j];
                }
            }
            return matArray;
        }


        /// <summary>
        /// Converts an OpenCvSharp Mat to a flat array (double[]) for DistCoeffs.
        /// </summary>
        private double[] ConvertMatToArray(Mat mat)
        {
            double[] matArray = new double[mat.Rows * mat.Cols];
            mat.GetArray(out matArray);
            return matArray;
        }

        /// <summary>
        /// Creates a list of 3D object points for the checkerboard.
        /// </summary>
        private List<Point3f> CreateObjectPoints(Size patternSize)
        {
            List<Point3f> points = new List<Point3f>();
            for (int i = 0; i < patternSize.Height; i++)
                for (int j = 0; j < patternSize.Width; j++)
                    points.Add(new Point3f(j, i, 0));
            return points;
        }

        public void Dispose()
        {
            CameraMatrix?.Dispose();
            DistCoeffs?.Dispose();
        }
    }
}
