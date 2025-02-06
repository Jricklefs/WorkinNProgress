using System;
using System.Threading;
using System.Windows;

namespace DartDetection
{
    public partial class InputDialog : Window
    {
        private static string lastEnteredText = "Image Name";

        public string ResponseText { get; private set; }
        public string SelectedCategory { get; private set; }
        public bool DisableBaseline { get; private set; }

        public InputDialog(string question, string defaultAnswer = "")
        {
            InitializeComponent();
            ResponseTextBox.Text = lastEnteredText;
            ResponseTextBox.Focus();
            ResponseTextBox.CaretIndex = ResponseTextBox.Text.Length; // Move cursor to the end
        }

        private void ResponseTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ResponseTextBox.Text == "Image Name")
            {
                ResponseTextBox.Clear();
            }
            ResponseTextBox.CaretIndex = ResponseTextBox.Text.Length; // Ensure cursor is at the end
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseTextBox.Clear();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = ResponseTextBox.Text;
            DisableBaseline = DisableBaselineCheckbox.IsChecked ?? false;
            SelectedCategory = GetSelectedCategory();
            lastEnteredText = ResponseText;
            DialogResult = true;
        }

        private string GetSelectedCategory()
        {
            if (radio1Dart.IsChecked == true)
            {
                return "1 Dart";
            }
            else if (radio2Darts.IsChecked == true)
            {
                return "2 Darts";
            }
            else if (radio3Darts.IsChecked == true)
            {
                return "3 Darts";
            }
            return string.Empty;
        }

        public static bool? ShowDialogOnSTA(string question, out string responseText, out string selectedCategory, out bool disableBaseline)
        {
            responseText = string.Empty;
            selectedCategory = string.Empty;
            disableBaseline = false;

            bool? dialogResult = null;

            string tempResponseText = string.Empty;
            string tempSelectedCategory = string.Empty;
            bool tempDisableBaseline = false;

            Thread staThread = new Thread(() =>
            {
                InputDialog dialog = new InputDialog(question);
                dialogResult = dialog.ShowDialog();

                if (dialogResult == true)
                {
                    tempResponseText = dialog.ResponseText;
                    tempSelectedCategory = dialog.SelectedCategory;
                    tempDisableBaseline = dialog.DisableBaseline;
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join(); // Wait for the thread to finish

            // Assign the local variables to the out parameters
            responseText = tempResponseText;
            selectedCategory = tempSelectedCategory;
            disableBaseline = tempDisableBaseline;

            return dialogResult;
        }
    }
}
