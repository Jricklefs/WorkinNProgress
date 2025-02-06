using DartDetection.Model_Logic;
using System.IO;

namespace DartDetection.Model_Logic
{
    /// <summary>
    /// If your model outputs a probability (e.g., between 0 and 1), you can assume a dart is detected if the probability is greater than 0.5.
    //You may need to adjust the index (Predictions[0]) based on how many output classes your model provides.If your model outputs a probability (e.g., between 0 and 1), you can assume a dart is detected if the probability is greater than 0.5.
    //You may need to adjust the index (Predictions[0]) based on how many output classes your model provides.
    /// </summary>
    public class PredictionService
    {
        private readonly IModelLoader _modelLoader;
        private readonly int _imageHeight;
        private readonly int _imageWidth;

        public PredictionService(string modelPath)
        {
            // Conditional switch: Load based on file extension
            if (Path.GetExtension(modelPath).ToLower() == ".onnx")
            {
                _modelLoader = new OnnxModelLoader(modelPath);
                _imageHeight = 512;  // ONNX model expects 512x512
                _imageWidth = 512;
            }
            else if (Path.GetExtension(modelPath).ToLower() == ".pb")
            {
                _modelLoader = new TensorFlowModelLoader(modelPath);
                _imageHeight = 512;  // TensorFlow model expects 512x512
                _imageWidth = 512;
            }
            else
            {
                throw new InvalidDataException("Unsupported model format. Use either '.pb' or '.onnx'.");
            }
        }

        public object PredictFromImage(string imagePath)
        {


            // Process the image with the correct dimensions based on the model
            var imageData = ImageProcessor.LoadAndProcessImage(imagePath, _imageWidth, _imageHeight);

            // Create OnnxModelInput or TensorFlowModelInput based on the model type
            if (_modelLoader is OnnxModelLoader)
            {
                // Pass OnnxModelInput for ONNX models
                var input = new OnnxModelInput
                {
                    ImageData = imageData
                };
                return _modelLoader.Predict(input);
            }
            else if (_modelLoader is TensorFlowModelLoader)
            {
                // Pass TensorFlowModelInput for TensorFlow models
                var input = new TensorFlowModelInput
                {
                    ImageData = imageData
                };
                return _modelLoader.Predict(input);
            }

            throw new InvalidDataException("Unsupported model type.");
        }
    }




}

