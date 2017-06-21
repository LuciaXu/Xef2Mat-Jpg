using KinectMLConnect;
using Microsoft.Kinect.Tools;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;

/***********************************************************************************************
 * The Orginal Version: Convert XEF to Mat (depth & IR)
 * Author:  Ray Litchi
 * Contact: raysworld@outlook.com
 * Date:    2016-11-07
 * Instructions:
 *  This app is used for converting Kinect Studio data file (.xef) to Matlab data file (.mat).
 *  The project is based on the following references:
 *      - Microsoft.Kinect (You may find it by installing Microsoft Kinect SDK 2.0)
 *      - Microsoft.Kinect.Tools (You may also find it by installing Microsoft Kinect SDK 2.0)
 *      - MATWriter (Written by SergentMT, which you may find here: http://www.codeproject.com/Tips/819613/Kinect-Version-Depth-Frame-to-mat-File-Exporter) 
 * Notes:
 *  It seems that this app only works on x64 platforms
 * How-to-use:
 *  Simply run xef2mat.exe, select the .xef file by clicking on the 'select' button. The output files
 *  will be at the same folder of the app.
************************************************************************************************/


/***********************************************************************************************
 * The Second Version: Convert XEF to JPG images (color) and Mat (depth & IR)
 * Author: Lu Xu
 * Contact: xulu_0908@hotmail.com
 * Data: 2017-02-16
 * Notes: 
 * Add a new function into this app, which can convert the color stream of xef to JPG images.
 *     - The format of color stream in xef is in YUY2. https://msdn.microsoft.com/en-us/library/windows/desktop/dd206750(v=vs.85).aspx
 *     - How to convert yuy2 to bgra: https://github.com/freestylecoder/Get-Kinect-ed/blob/master/1.5%20-%20Color%20Camera%20(Parallel)/MainWindow.xaml.cs
 * Add an absolute timestamp of each stream and save it in txt files.
 ***********************************************************************************************/

namespace XEF2MAT
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {        
        private const int WIDTH = 512;          // Width of the depth image
        private const int HEIGHT = 424;         // Height of the depth image

        private const int COLOR_WIDTH = 1920;          // Width of the color image
        private const int COLOR_HEIGHT = 1080;         // Height of the color image

        private string fileName = null;         // File name of the imported .xef file

        private int frameCount = 0;             // The number of frames

        private ushort[] color_timing = null;         // Storage the time stamp of the color frames
        private ushort[] depth_timing = null;         // Storage the time stamp of the depth frames
        private ushort[] IR_timing = null;         // Storage the time stamp of the IR frames

        private ushort[] outputData = null;     // Storage the Depth and IR data of the frames

        private byte[] colorData = null;        //Storage the Raw Color data of the frames
        private byte[] colorRGBA = null;        //Storage the RGBA data of the frames

        private System.DateTime start_time;
        
        private string state = null;            // Current process state


        private BackgroundWorker b;             // Handle the export process in background

        private bool extractDepth = false;
        private bool extractIR = false;
        private bool extractColor = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            b = new BackgroundWorker();
            b.WorkerReportsProgress = true;

            b.DoWork += DirtyWork;
            b.ProgressChanged += B_ProgressChanged;
            b.RunWorkerCompleted += B_RunWorkerCompleted;
        }

        private void B_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int progress = e.ProgressPercentage;

            progressBar.Value = progress;
            label3.Content = state;
        }

        private void B_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            label3.Content = "Completed!";

            button.IsEnabled = true;
            button.Content = "Select";
        }

        public static void Copy(IntPtr source, ushort[] destination, int startIndex, int length)
        {
            unsafe
            {
                var sourcePtr = (ushort*)source;
                for (int i = startIndex; i < startIndex + length; ++i)
                {
                    destination[i] = *sourcePtr++;
                }
            }
        }

        public static void Copy(IntPtr source, byte[] destination, int startIndex, int length)
        {
            unsafe
            {
                var sourcePtr = (byte*)source;
                for (int i = startIndex; i < startIndex + length; ++i)
                {
                    destination[i] = *sourcePtr++;
                }
            }
        }


        private void button_Click(object sender, RoutedEventArgs e)
        {
            var folder_path = Environment.CurrentDirectory + "/Xef2Mat_Output";
            if (!Directory.Exists(folder_path))
            {
                Directory.CreateDirectory(folder_path);
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.CurrentDirectory;
            openFileDialog.Filter = "Kinect Studio Data File|*.xef";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            var result = openFileDialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                fileName = openFileDialog.FileName;
                
                button.IsEnabled = false;
                button.Content = "Working...";
                b.RunWorkerAsync();
            }
            else
            {
                return;
            }
        }

        private byte ClipToByte(int p_ValueToClip)
        {

            return Convert.ToByte((p_ValueToClip < byte.MinValue) ? byte.MinValue : ((p_ValueToClip > byte.MaxValue) ? byte.MaxValue : p_ValueToClip));

        }

        private void ToTXTFile(string filepath, DateTime start_time, ushort[] timing)
        {
            System.IO.StreamWriter file = new System.IO.StreamWriter(filepath);
            foreach(ushort time in timing)
            {
                DateTime newtime = start_time.AddMilliseconds(time);
                file.WriteLine(newtime.ToString("yyMMddHmmssfff"));
            }
        }

        private void DirtyWork(object sender, DoWorkEventArgs e)
        {
            outputData = new ushort[WIDTH * HEIGHT];
            colorData = new byte[COLOR_WIDTH * COLOR_HEIGHT * 2];
            colorRGBA = new byte[COLOR_WIDTH * COLOR_HEIGHT * 4];

            var client = KStudio.CreateClient();

            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }
            
            var file = client.OpenEventFile(fileName);
            foreach(KStudioFileInfo fileinfo in client.GetFileList(file.FilePath))
            {
                 start_time = fileinfo.CreationUtcFileTime;
            }


            foreach (var item in file.EventStreams)
            {
                if ((item.DataTypeName.Equals("Nui Depth"))&&(extractDepth))
                {
                    state = "Depth";

                    KStudioSeekableEventStream stream = item as KStudioSeekableEventStream;
                    this.frameCount = (int)stream.EventCount;
                    depth_timing = new ushort[frameCount];

                    //unsafe
                    int size = outputData.Length * sizeof(ushort);
                    IntPtr ip = Marshal.AllocHGlobal(size);

                    for (uint i = 0; i < frameCount; i++)
                    {
                        b.ReportProgress((int)((float)i / frameCount * 100));

                        Thread.Sleep(100);
                        using(var curr_event = stream.ReadEvent(i))
                        {
                           
                            {
                                uint bufferSize = 0;
                                curr_event.AccessUnderlyingEventDataBuffer(out bufferSize, out ip);

                                Copy(ip, outputData, 0, outputData.Length);
                            }
                            this.depth_timing[i] = (ushort)curr_event.RelativeTime.TotalMilliseconds;
                            string filePath = Environment.CurrentDirectory + "/Xef2Mat_Output/DepthFrame" + i.ToString("D4") + ".mat";
                            MATWriter.ToMatFile("Dep" + i.ToString("D4"), filePath, outputData, HEIGHT, WIDTH);
                        }

                    }
                }
                if ((item.DataTypeName.Equals("Nui Uncompressed Color"))&&(extractColor))
                {
                    state = "Color";

                    KStudioSeekableEventStream stream = item as KStudioSeekableEventStream;
                    this.frameCount = (int)stream.EventCount;
                    color_timing = new ushort[frameCount];

                    //unsafe
                    //{
                    int size = colorData.Length * sizeof(byte) * 2;
                    IntPtr ip = Marshal.AllocHGlobal(size);
                    //}

                    for (uint i = 0; i < frameCount; i++)
                    {
                        b.ReportProgress((int)((float)i / frameCount * 100));

                        Thread.Sleep(100);
                        using (var curr_event = stream.ReadEvent(i))
                        {

                            uint bufferSize = 0;
                            curr_event.AccessUnderlyingEventDataBuffer(out bufferSize, out ip);

                            Copy(ip, colorData, 0, colorData.Length);


                        for(int _Index =0; _Index< (COLOR_WIDTH*COLOR_HEIGHT)/2; _Index++)
                        {
                            int _Y0 = colorData[(_Index << 2) + 0] - 16;
                            int _U = colorData[(_Index << 2) + 1] - 128;
                            int _Y1 = colorData[(_Index << 2) + 2] - 16;
                            int _V = colorData[(_Index << 2) + 3] - 128;

                            byte _R = ClipToByte((298 * _Y0 + 409 * _V + 128) >> 8);
                            byte _G = ClipToByte((298 * _Y0 - 100 * _U - 208 * _V + 128) >> 8);
                            byte _B = ClipToByte((298 * _Y0 + 516 * _U + 128) >> 8);

                            colorRGBA[(_Index << 3) + 0] = _B;
                            colorRGBA[(_Index << 3) + 1] = _G;
                            colorRGBA[(_Index << 3) + 2] = _R;
                            colorRGBA[(_Index << 3) + 3] = 0xFF; // A

                            _R = ClipToByte((298 * _Y1 + 409 * _V + 128) >> 8);
                            _G = ClipToByte((298 * _Y1 - 100 * _U - 208 * _V + 128) >> 8);
                            _B = ClipToByte((298 * _Y1 + 516 * _U + 128) >> 8);

                            colorRGBA[(_Index << 3) + 4] = _B;
                            colorRGBA[(_Index << 3) + 5] = _G;
                            colorRGBA[(_Index << 3) + 6] = _R;
                            colorRGBA[(_Index << 3) + 7] = 0xFF;
                        }
                        int stride = (PixelFormats.Bgr32.BitsPerPixel) * 1920 / 8;
                        BitmapSource bmpSource = BitmapSource.Create(1920, 1080, 96.0, 96.0, PixelFormats.Bgr32, null, colorRGBA, stride);
                        JpegBitmapEncoder c_encoder = new JpegBitmapEncoder();
                        c_encoder.Frames.Add(BitmapFrame.Create(bmpSource));
                        using (var fs = new FileStream(Environment.CurrentDirectory + "/Xef2Mat_Output/ColorFrame" + i.ToString("D4") + ".jpg", FileMode.Create))
                        {
                            c_encoder.Save(fs);
                        }

                        this.color_timing[i] = (ushort)curr_event.RelativeTime.TotalMilliseconds;
                    }
                    }
                }

                if ((item.DataTypeName.Equals("Nui IR"))&&(extractIR))
                {
                    state = "IR";

                    KStudioSeekableEventStream stream = item as KStudioSeekableEventStream;
                    this.frameCount = (int)stream.EventCount;
                    IR_timing = new ushort[frameCount];

                    //unsafe
                    int size = outputData.Length * sizeof(ushort);
                    IntPtr ip = Marshal.AllocHGlobal(size);

                    for (uint i = 0; i < frameCount; i++)
                    {
                        b.ReportProgress((int)((float)i / frameCount * 100));

                        using(var curr_event = stream.ReadEvent(i))
                        {
                            
                            {
                                
                                uint bufferSize = 0;
                                curr_event.AccessUnderlyingEventDataBuffer(out bufferSize, out ip);

                                Copy(ip, outputData, 0, outputData.Length);
                            }
                            this.IR_timing[i] = (ushort)curr_event.RelativeTime.TotalMilliseconds;
                            string filePath = Environment.CurrentDirectory + "/Xef2Mat_Output/IRFrame" + i.ToString("D4") + ".mat";
                            MATWriter.ToMatFile("IR" + i.ToString("D4"), filePath, outputData, HEIGHT, WIDTH);
                        }

                    }
                }
            }
            if (frameCount > 0)
            {
                state = "TimeStamp";
                b.ReportProgress(100);
                if(depth_timing!=null)
                {
                    string filePath = Environment.CurrentDirectory + "/Xef2Mat_Output/TimeStamp_depth.mat";
                    MATWriter.ToMatFile("Time", filePath, this.depth_timing, this.depth_timing.Length, 1);
                    string abfilepath = Environment.CurrentDirectory + "/Xef2Mat_Output/Absolute_Time_depth.txt";
                    ToTXTFile(abfilepath,start_time,depth_timing);
                }
                if (color_timing != null)
                {
                    string filePath = Environment.CurrentDirectory + "/Xef2Mat_Output/TimeStamp_color.mat";
                    MATWriter.ToMatFile("Time", filePath, this.color_timing, this.color_timing.Length, 1);
                    string abfilepath = Environment.CurrentDirectory + "/Xef2Mat_Output/Absolute_Time_color.txt";
                    ToTXTFile(abfilepath, start_time, color_timing);
                }
                if (IR_timing != null)
                {
                    string filePath = Environment.CurrentDirectory + "/Xef2Mat_Output/TimeStamp_IR.mat";
                    MATWriter.ToMatFile("Time", filePath, this.IR_timing, this.IR_timing.Length, 1);
                    string abfilepath = Environment.CurrentDirectory + "/Xef2Mat_Output/Absolute_Time_IR.txt";
                    ToTXTFile(abfilepath, start_time, IR_timing);
                }

            }
        }

        private void DepthBox_Checked(object sender, RoutedEventArgs e)
        {
           extractDepth = true;

        }

        private void ColorBox_Checked(object sender, RoutedEventArgs e)
        {
            extractColor = true;

        }

        private void IRBox_Checked(object sender, RoutedEventArgs e)
        {
           extractIR = true;
        }

        private void DepthBox_Unchecked(object sender, RoutedEventArgs e)
        {
            extractDepth = false;
        }

        private void IRBox_Unchecked(object sender, RoutedEventArgs e)
        {
            extractIR = false;
        }

        private void ColorBox_Unchecked(object sender, RoutedEventArgs e)
        {
            extractColor = false;
        }


    }
}
