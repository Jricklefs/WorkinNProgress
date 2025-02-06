using Microsoft.ML.Data;

namespace DartDetection.Model_Logic
{
    public class TensorFlowModelInput
    {
        // TensorFlow expects input column name 'x'
        [ColumnName("x")]
        [VectorType(512, 512, 1)]
        public float[] ImageData { get; set; }
    }

    public class TensorFlowModelOutput
    {
        // TensorFlow model outputs 'Identity'
        [ColumnName("Identity")]
        [VectorType(1)]
        public float[] Predictions { get; set; }
    }

    public class OnnxModelInput
    {
        // ONNX expects input column name 'inputs'
        [ColumnName("input_image")]
        [VectorType(1, 512, 512, 1)]  // Adjust for ONNX model input shape
        public float[] ImageData { get; set; }
    }

    public class OnnxModelOutput
    {
        // ONNX model outputs 'output_0'
        [ColumnName("conv2d_18")]
        [VectorType(1)]
        public float[] Predictions { get; set; }
    }

}
