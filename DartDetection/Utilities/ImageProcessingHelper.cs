using OpenCvSharp;
using System;

namespace DartDetection.Utilities
{
    public static class ImageProcessingHelper
    {
        private static AppSettings _appSettings;

        /// <summary>
        /// Loads camera calibration settings from AppSettings.
        /// </summary>
        public static void LoadCalibrationSettings()
        {
            _appSettings = SettingsManager.LoadSettings();
        }

        /// <summary>
        /// Applies radial distortion correction to an image using camera calibration data.
        /// </summary>
        public static Mat CorrectRadialDistortion(Mat inputImage)
        {
            if (_appSettings == null)
            {
                Console.WriteLine("Error: AppSettings not loaded. Call LoadCalibrationSettings first.");
                return inputImage;
            }

            if (_appSettings.CameraMatrix == null || _appSettings.DistCoeffs == null)
            {
                Console.WriteLine("Error: Camera calibration data is missing.");
                return inputImage; // Return original image if no calibration data
            }

            // Convert jagged array to Mat
            Mat cameraMatrix = new Mat(3, 3, MatType.CV_64F);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    cameraMatrix.Set(i, j, _appSettings.CameraMatrix[i][j]);
                }
            }

            Mat distCoeffs = new Mat(1, _appSettings.DistCoeffs.Length, MatType.CV_64F);
            distCoeffs.SetArray(_appSettings.DistCoeffs);

            // Apply distortion correction
            Mat undistortedImage = new Mat();
            Cv2.Undistort(inputImage, undistortedImage, cameraMatrix, distCoeffs);

            return undistortedImage;
        }
    }
}
