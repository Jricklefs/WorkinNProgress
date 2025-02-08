using System.Windows;

namespace DartDetection
{
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Event handler for the camera window
        private void OpenCameraWindow_Click(object sender, RoutedEventArgs e)
        {
            CameraWindow cameraWindow = new CameraWindow();
            cameraWindow.Show();
        }

        // Event handler for the dartboard detection window
        private void OpenDartboardWindow_Click(object sender, RoutedEventArgs e)
        {
            DartboardDetectionWindow dartboardWindow = new DartboardDetectionWindow();
            dartboardWindow.Show();
        }

        // Event handler for the dartboard configuration page
        private void OpenDartboardConfig_Click(object sender, RoutedEventArgs e)
        {
            DartboardConfigPage configPage = new DartboardConfigPage();
            configPage.Show();
        }


        private void CameraFeedButton_Click(object sender, RoutedEventArgs e)
        {
            CameraFeedWindow configPage = new CameraFeedWindow();
            configPage.Show();
        }
        private void ZooFly_Click(object sender, RoutedEventArgs e)
        {
            ZF2 configPage = new ZF2();
            configPage.Show();
        }

    }
}
