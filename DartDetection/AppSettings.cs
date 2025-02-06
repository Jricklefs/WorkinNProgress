using DartDetection.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DartDetection
{
    public  class AppSettings
    {
        // Settings for the Polar Graph.
        public string PolarGraphImagePath { get; set; } = "Polar_Graph_Paper.png";
        public double PolarGraphDefaultZoom { get; set; } = 1.0;
        public int PolarGraphBaseSize { get; set; } = 400;

        // The 4 points on the Polar Graph used as destination reference.
        // Expecting exactly 4 points (top-left, top-right, bottom-right, bottom-left).
        public List<PointData> PolarGraphPoints { get; set; } = new List<PointData>();

        // A collection of camera settings. Each camera can have its own set of 4 source points.
        public List<CameraSettings> Cameras { get; set; } = new List<CameraSettings>();

    }
}
