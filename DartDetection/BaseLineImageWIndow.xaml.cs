using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DartDetection
{
    public partial class BaselineImageWindow : System.Windows.Window
    {
        public ObservableCollection<CameraBaselineModel> CameraBaselines { get; set; }

        public BaselineImageWindow()
        {
            InitializeComponent();
            CameraBaselines = new ObservableCollection<CameraBaselineModel>();
            ImagesPanel.ItemsSource = CameraBaselines;
        }

        public void SetBaselineImages(int cameraIndex, BitmapSource baselineImage, BitmapSource calibratedBaselineImage = null)
        {
            if (cameraIndex < CameraBaselines.Count)
            {
                // Update only the BaselineImage unless a new CalibratedBaselineImage is provided
                CameraBaselines[cameraIndex].BaselineImage = baselineImage;

                if (calibratedBaselineImage != null)
                {
                    CameraBaselines[cameraIndex].CalibratedBaselineImage = calibratedBaselineImage;
                }
            }
            else
            {
                // Add new entry with both BaselineImage and CalibratedBaselineImage
                CameraBaselines.Add(new CameraBaselineModel
                {
                    CameraLabel = $"Camera {cameraIndex + 1}",
                    BaselineImage = baselineImage,
                    CalibratedBaselineImage = calibratedBaselineImage
                });
            }
        }

    }

    public class CameraBaselineModel : INotifyPropertyChanged
    {
        private string _cameraLabel;
        private BitmapSource _baselineImage;
        private BitmapSource _calibratedBaselineImage;

        public string CameraLabel
        {
            get => _cameraLabel;
            set
            {
                _cameraLabel = value;
                OnPropertyChanged();
            }
        }

        public BitmapSource BaselineImage
        {
            get => _baselineImage;
            set
            {
                _baselineImage = value;
                OnPropertyChanged();
            }
        }

        public BitmapSource CalibratedBaselineImage
        {
            get => _calibratedBaselineImage;
            set
            {
                _calibratedBaselineImage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
