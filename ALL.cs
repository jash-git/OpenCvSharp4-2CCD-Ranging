/*
一、前置條件（非常關鍵）
	1️棋盤格規格（你要準備）

		內角點：9 x 6（常用）

		每格尺寸：例如 20 mm


	2拍攝規則（影響精度）

		左右相機「固定不動」

		同時拍攝（同步）

		15～25組不同角度

		棋盤格覆蓋畫面各區域
*/

		
		
//二、Part A：雙目校正程式
using OpenCvSharp;
using System;
using System.Collections.Generic;

class StereoCalib
{
    static void Main()
    {
        Size boardSize = new Size(9, 6);
        float squareSize = 20.0f; // mm

        var objPoints = new List<Point3f[]>();
        var imgPointsL = new List<Point2f[]>();
        var imgPointsR = new List<Point2f[]>();

        // === 建立世界座標棋盤格 ===
        var objp = new List<Point3f>();
        for (int i = 0; i < boardSize.Height; i++)
            for (int j = 0; j < boardSize.Width; j++)
                objp.Add(new Point3f(j * squareSize, i * squareSize, 0));

        // === 載入影像（自行準備）===
        string[] leftImgs = System.IO.Directory.GetFiles("left_calib", "*.jpg");
        string[] rightImgs = System.IO.Directory.GetFiles("right_calib", "*.jpg");

        for (int i = 0; i < leftImgs.Length; i++)
        {
            Mat imgL = Cv2.ImRead(leftImgs[i], ImreadModes.Grayscale);
            Mat imgR = Cv2.ImRead(rightImgs[i], ImreadModes.Grayscale);

            if (Cv2.FindChessboardCorners(imgL, boardSize, out Point2f[] cornersL) &&
                Cv2.FindChessboardCorners(imgR, boardSize, out Point2f[] cornersR))
            {
                imgPointsL.Add(cornersL);
                imgPointsR.Add(cornersR);
                objPoints.Add(objp.ToArray());
            }
        }

        Mat K1 = new Mat(), D1 = new Mat();
        Mat K2 = new Mat(), D2 = new Mat();
        Mat R = new Mat(), T = new Mat(), E = new Mat(), F = new Mat();

        Cv2.StereoCalibrate(
            objPoints,
            imgPointsL,
            imgPointsR,
            K1, D1,
            K2, D2,
            new Size(640, 480),
            R, T, E, F,
            CalibrationFlags.None
        );

        // === Stereo Rectify ===
        Mat R1 = new Mat(), R2 = new Mat();
        Mat P1 = new Mat(), P2 = new Mat();
        Mat Q = new Mat();

        Cv2.StereoRectify(
            K1, D1,
            K2, D2,
            new Size(640, 480),
            R, T,
            R1, R2, P1, P2, Q
        );

        Console.WriteLine("Calibration Done");

        // 👉 建議：把 K1,K2,D1,D2,R,T,Q 存檔（XML/YAML）
    }
}



//三、Part B：影像校正（Rectification）
Mat map1x, map1y, map2x, map2y;

Cv2.InitUndistortRectifyMap(K1, D1, R1, P1, imageSize, MatType.CV_32FC1, out map1x, out map1y);
Cv2.InitUndistortRectifyMap(K2, D2, R2, P2, imageSize, MatType.CV_32FC1, out map2x, out map2y);

Mat rectL = new Mat(), rectR = new Mat();

Cv2.Remap(imgL, rectL, map1x, map1y, InterpolationFlags.Linear);
Cv2.Remap(imgR, rectR, map2x, map2y, InterpolationFlags.Linear);



四、Part C：視差 → 3D（核心）
//1計算視差
var stereo = StereoSGBM.Create(0, 16 * 6, 5);

Mat disp = new Mat();
stereo.Compute(rectL, rectR, disp);

Mat disp32 = new Mat();
disp.ConvertTo(disp32, MatType.CV_32F, 1.0 / 16);


//2轉換 3D（用 Q matrix）
Mat points3D = new Mat();
Cv2.ReprojectImageTo3D(disp32, points3D, Q);

//3️取得某點距離
Vec3f pt = points3D.At<Vec3f>(y, x);

float X = pt.Item0;
float Y = pt.Item1;
float Z = pt.Item2; // 深度

double distance = Math.Sqrt(X*X + Y*Y + Z*Z);

Console.WriteLine($"距離 = {distance} mm");