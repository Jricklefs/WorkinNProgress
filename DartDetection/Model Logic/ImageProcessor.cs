using OpenCvSharp;
namespace DartDetection.Model_Logic
{
    public class ImageProcessor
    {
        public static float[] LoadAndProcessImage(string imagePath, int width = 512, int height = 512)
        {
            // Load image in grayscale using OpenCV
            using var mat = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
            Cv2.Resize(mat, mat, new Size(width, height)); // Resize to match model input

            // Normalize image and convert to float array
            float[] imageData = new float[width * height * 1]; // Single channel (grayscale)
            int index = 0;
            for (int y = 0; y < mat.Rows; y++)
            {
                for (int x = 0; x < mat.Cols; x++)
                {
                    byte pixelValue = mat.At<byte>(y, x);
                    imageData[index++] = pixelValue / 255f; // Normalize pixel value to [0, 1]
                }
            }

            return imageData;
        }
    }

}
