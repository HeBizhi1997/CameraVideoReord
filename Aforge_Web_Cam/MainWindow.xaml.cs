using System.Windows;

namespace CameraVideoRecord
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = VideoRecord.GetVideoRecord();
        }

        private void StrBtn_Click(object sender, RoutedEventArgs e)
        {
            VideoRecord.GetVideoRecord().StartRecording();
        }

        private void EndBtn_Click(object sender, RoutedEventArgs e)
        {
            VideoRecord.GetVideoRecord().EndRecording();
        }

        private void ScreenshotsBtn_Click(object sender, RoutedEventArgs e)
        {
            VideoRecord.GetVideoRecord().SaveSnapshot();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            VideoRecord.GetVideoRecord().EndRecording();
        }
    }
}
