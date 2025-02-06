This is how to implemnt the 


using System;

class Program
{
    static void Main(string[] args)
    {
        // Define the model path and input/output node names
        string modelPath = "AIModels/dart_segmentation_144_unet_grayscale_saved_model_TF/your_model.pb";
        string inputNodeName = "input_1"; // Replace with your model's input node name
        string outputNodeName = "predictions"; // Replace with your model's output node name

        // Create the prediction service
        var predictionService = new PredictionService(modelPath, inputNodeName, outputNodeName);

        // Provide the image path
        string imagePath = "path_to_image.jpg";

        // Predict from the image
        var prediction = predictionService.PredictFromImage(imagePath);

        // Display predictions
        foreach (var p in prediction.Predictions)
        {
            Console.WriteLine($"Prediction: {p}");
        }
    }
}



//The Dart_segmentation_144_unet_grayscale_savedModel_TF  has the folloiwin nodes. 

Input Signature:
css
Copy code
Input Signature: ((), {'inputs': TensorSpec(shape=(None, 512, 512, 1), dtype=tf.float32, name='inputs')})
This means that your model expects an input named inputs with the following characteristics:
Shape: (None, 512, 512, 1)
The None means the batch size is flexible (you can input multiple images in a batch or a single image).
The model expects images of size 512x512 with 1 channel (i.e., grayscale images).
Data Type: tf.float32 (32-bit floating-point).
Output Signature:
css
Copy code
Output Signature: {'output_0': TensorSpec(shape=(None, 512, 512, 1), dtype=tf.float32, name='output_0')}
This means that your model produces an output named output_0 with the following characteristics:
Shape: (None, 512, 512, 1)
Similar to the input, the output has a flexible batch size (None), and for each input image, the output will also be of size 512x512 with 1 channel (grayscale).
Data Type: tf.float32 (32-bit floating-point).