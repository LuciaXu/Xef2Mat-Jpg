# Xef2Mat-Jpg
A wpf based programme that can convert kinect xef files into mat (for depth and ir), and jpg images (for color stream)

## How to Use:
1. Download the repository.
2. Go to "Xef2Mat&JPG/bin"
3. Run the exe file
4. Select the file format before choose the XEF file.
5. Wait for complete.
6. Find the files under "Xef2Mat&JPG/bin/Xef2Mat_Output"

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
