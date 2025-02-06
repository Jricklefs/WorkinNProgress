using DartDetection.Model_Logic;
using Microsoft.ML;
//using Microsoft.ML.Transforms.TensorFlow;
using System.Collections.Generic;
using System.IO;

namespace DartDetection.Model_Logic
{

    public class TensorFlowModelLoader : IModelLoader
    {
        private readonly PredictionEngine<TensorFlowModelInput, TensorFlowModelOutput> _predictionEngine;

        public TensorFlowModelLoader(string modelPath)
        {
            var mlContext = new MLContext();

            // Load the TensorFlow model
            var tensorflowModel = mlContext.Model.LoadTensorFlowModel(modelPath);

            // Define the input and output node names based on your TensorFlow model
            var pipeline = tensorflowModel.ScoreTensorFlowModel(
                outputColumnNames: new[] { "Identity" },  // Your model's output node name
                inputColumnNames: new[] { "x" },          // Your model's input node name
                addBatchDimensionInput: true              // Set to true if your model expects a batch dimension
            );

            // Create a prediction pipeline and engine
            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<TensorFlowModelInput>()));
            _predictionEngine = mlContext.Model.CreatePredictionEngine<TensorFlowModelInput, TensorFlowModelOutput>(model);
        }

        public object Predict(object input)
        {
            if (input is TensorFlowModelInput tfInput)
            {
                return _predictionEngine.Predict(tfInput);
            }

            throw new InvalidDataException("Invalid input type for TensorFlow model.");
        }
    }
}
