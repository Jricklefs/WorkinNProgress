using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DartDetection.Model_Logic
{
    public  interface IModelLoader
    {
        object Predict(object input);
    }
}
