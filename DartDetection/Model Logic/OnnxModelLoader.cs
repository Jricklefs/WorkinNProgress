using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tensorflow;

namespace DartDetection.Model_Logic
{
    public class OnnxModelLoader : IModelLoader
    {
        private readonly PredictionEngine<OnnxModelInput, OnnxModelOutput> _predictionEngine;

        public OnnxModelLoader(string modelPath)
        {
            var mlContext = new MLContext();

            // Load the ONNX model
            var pipeline = mlContext.Transforms.ApplyOnnxModel(
                modelFile: modelPath,
                //outputColumnNames: new[] { "output_0" },  // Update if your ONNX model has different output node names
                //inputColumnNames: new[] { "inputs" }           // Update if your ONNX model has different input node names
                outputColumnNames: new[] { "conv2d_18" },  // Update if your ONNX model has different output node names
                inputColumnNames: new[] { "input_image" }           // Update if your ONNX model has different input node names
            );

            // Create a prediction pipeline and engine
            var model = pipeline.Fit(mlContext.Data.LoadFromEnumerable(new List<OnnxModelInput>()));
            _predictionEngine = mlContext.Model.CreatePredictionEngine<OnnxModelInput, OnnxModelOutput>(model);
        }

        public object Predict(object input)
        {
            if (input is OnnxModelInput onnxInput)
            {
                return _predictionEngine.Predict(onnxInput);
            }

            throw new InvalidDataException("Invalid input type for ONNX model.");
        }
    }
}
