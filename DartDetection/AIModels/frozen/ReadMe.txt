


To Visualize the Model use https://netron.app/


You will need to add the "frozen_model.pb" to this location.  or else comment out the 2 places in the Camera WIndow.Cs where it is being used. 
The file is 2 big for github. 



you will also need the  Onnx output model "output_model.onnx" if you plan on using onnx. 


Createing Onnx 
used python 3.8 and 
set up a Venv   (virtual Environment)
Instelled TensorFlow and tf2conn

converted to onnx with 

cmd  line call. 
python -m tf2onnx.convert --saved-model "path/to/your_model_directory" --output "path/to/output_model.onnx" --opset 13