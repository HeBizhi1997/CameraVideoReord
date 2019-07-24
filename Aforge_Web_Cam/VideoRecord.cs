using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Accord.Video.FFMPEG;
using AForge.Video;
using AForge.Video.DirectShow;

namespace CameraVideoRecord
{
    public enum VideoRecordStatus
    {
        Recording,
        End
    }

    public enum CameraSource
    {
        Desktop,
        LocalCamera,
        Webcam
    }

    public class VideoRecord : INotifyPropertyChanged
    {
        private static VideoRecord videoRecord = new VideoRecord();
        private VideoRecord()
        {
            GetVideoDevices();
        }
        public static VideoRecord GetVideoRecord() { return videoRecord; }



        /// <summary>
        /// 本机的摄像设备列表
        /// </summary>
        public ObservableCollection<FilterInfo> VideoDevices { get; set; } = new ObservableCollection<FilterInfo>();
        /// <summary>
        /// 当前选中的摄像模式
        /// </summary>
        public CameraSource Camera
        {
            get { return _camera; }
            set
            {
                _camera = value;
                this.OnPropertyChanged("Camera");
            }
        }
        private CameraSource _camera = CameraSource.Desktop;
        /// <summary>
        /// 当前录制状态
        /// </summary>
        public VideoRecordStatus RecordStatus
        {
            get { return _recordStatus; }
            set
            {
                _recordStatus = value;
                this.OnPropertyChanged("RecordStatus");
            }
        }
        private VideoRecordStatus _recordStatus = VideoRecordStatus.End;
        /// <summary>
        /// 当前选中的本机摄像设备
        /// </summary>
        public FilterInfo CurrentDevice
        {
            get { return _currentDevice; }
            set
            {
                _currentDevice = value;
                this.OnPropertyChanged("CurrentDevice");
            }
        }
        private FilterInfo _currentDevice;
        /// <summary>
        /// 当前图像
        /// </summary>
        public BitmapImage Image
        {
            get { return _image; }
            private set
            {
                _image = value;
                this.OnPropertyChanged("Image");
            }
        }
        private BitmapImage _image;
        /// <summary>
        /// 网络摄像机地址
        /// </summary>
        public string WebcamUrl
        {
            get { return _webcamUrl; }
            set
            {
                _webcamUrl = value;
                this.OnPropertyChanged("VideoFilePath");
            }
        }
        private string _webcamUrl;
        /// <summary>
        /// 录像保存路径
        /// </summary>
        public string VideoFilePath
        {
            get { return _videoFilePath; }
            set
            {
                _videoFilePath = value;
                this.OnPropertyChanged("VideoFilePath");
            }
        }
        private string _videoFilePath = "Video01.avi";
        /// <summary>
        /// 截图保存路径
        /// </summary>
        public string ScreenshotsPath
        {
            get { return _screenshotsPath; }
            set
            {
                _screenshotsPath = value;
                this.OnPropertyChanged("VideoFilePath");
            }
        }
        private string _screenshotsPath = "Screenshots01.png";



        private IVideoSource _videoSource;
        private VideoFileWriter _writer;
        private DateTime? _firstFrameTime;


        /// <summary>
        /// 开始录像
        /// </summary>
        public void StartRecording()
        {
            if (RecordStatus == VideoRecordStatus.End)
            {
                //打开摄像头
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    OpenCamera();
                });

                _writer = new VideoFileWriter();
                if (Camera == CameraSource.Desktop)
                {
                    //获取当前屏幕尺寸,开始录制
                    var rect = System.Windows.Forms.Screen.AllScreens.First().Bounds;
                    _writer.Open(VideoFilePath, rect.Width, rect.Height);
                }
                else
                {
                    if (Camera == CameraSource.LocalCamera && VideoDevices.Count <= 0)
                    {
                        return;
                    }
                    //等待摄像头准备完毕
                    while (Image == null)
                    {
                        Thread.Sleep(100);
                    }
                    //传入摄像头录制的图片尺寸,开始录制
                    _writer.Open(VideoFilePath, Image.PixelWidth, Image.PixelHeight, 120);
                }
                RecordStatus = VideoRecordStatus.Recording;
            }
        }

        /// <summary>
        /// 结束录像
        /// </summary>
        public void EndRecording()
        {
            if (RecordStatus == VideoRecordStatus.Recording)
            {
                StopCamera();
                RecordStatus = VideoRecordStatus.End;
                _writer.Close();
                _writer.Dispose();
            }
        }

        /// <summary>
        /// 保存截图
        /// </summary>
        public void SaveSnapshot()
        {
            if (RecordStatus == VideoRecordStatus.Recording)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(Image));
                using (var filestream = new FileStream(ScreenshotsPath, FileMode.OpenOrCreate))
                {
                    encoder.Save(filestream);
                }
            }
        }


        /// <summary>
        /// 打开摄像头
        /// </summary>
        private void OpenCamera()
        {
            switch (Camera)
            {
                case CameraSource.Desktop:
                    _videoSource = new ScreenCaptureStream(System.Windows.Forms.Screen.AllScreens.First().Bounds);
                    _videoSource.NewFrame += video_NewFrame;
                    _videoSource.Start();
                    break;
                case CameraSource.LocalCamera:
                    if (CurrentDevice != null)
                    {
                        _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                        _videoSource.NewFrame += video_NewFrame;
                        _videoSource.Start();
                    }
                    else
                    {
                        MessageBox.Show("Current device can't be null");
                    }
                    break;
                case CameraSource.Webcam:
                    if (!string.IsNullOrEmpty(WebcamUrl))
                    {
                        _videoSource = new MJPEGStream(WebcamUrl);
                        _videoSource.NewFrame += video_NewFrame;
                        _videoSource.Start();
                    }
                    else
                    {
                        MessageBox.Show("WebcamUrl can't be null");
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 关闭摄像头
        /// </summary>
        private void StopCamera()
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= video_NewFrame;
            }
            Image = null;
        }

        /// <summary>
        /// 摄像头每一帧触发的事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                //写入录像数据流
                if (RecordStatus == VideoRecordStatus.Recording)
                {
                    using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                    {
                        try
                        {
                            if (_firstFrameTime != null)
                            {
                                _writer.WriteVideoFrame(bitmap, DateTime.Now - _firstFrameTime.Value);
                            }
                            else
                            {
                                _writer.WriteVideoFrame(bitmap);
                                _firstFrameTime = DateTime.Now;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
                //更新Image
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    var bi = bitmap.ToBitmapImage();
                    bi.Freeze();
                    Dispatcher.CurrentDispatcher.Invoke(() => Image = bi);
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error on _videoSource_NewFrame:\n" + exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopCamera();
            }
        }

        /// <summary>
        /// 获取本机的摄像设备
        /// </summary>
        private void GetVideoDevices()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in devices)
            {
                VideoDevices.Add(device);
            }
            if (VideoDevices.Any())
            {
                CurrentDevice = VideoDevices[0];
            }
            else
            {
                MessageBox.Show("No webcam found");
            }
        }



        #region INotifyPropertyChanged members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion
    }

    static class BitmapHelpers
    {
        public static BitmapImage ToBitmapImage(this Bitmap bitmap)
        {
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            bi.StreamSource = ms;
            bi.EndInit();
            return bi;
        }
    }
}
