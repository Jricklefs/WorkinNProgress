using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DartDetection.Models
{
    public class CameraSettings
    {
        // For example, a name or ID for the camera.
        public string CameraName { get; set; }

        // The 4 transformation (source) points for this camera.
        public List<PointData> TransformationPoints { get; set; } = new List<PointData>();
    }
}
