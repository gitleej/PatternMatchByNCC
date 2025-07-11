using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace PatternMatchByNCC
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool debug = false; // 是否开启调试模式

            // 加载源图像和目标图像
            Mat src = Cv2.ImRead("F:\\02-Code\\Fastest_Image_Pattern_Matching\\Test Images\\Src4.bmp",
                ImreadModes.Grayscale);
            Mat dst = Cv2.ImRead("F:\\02-Code\\Fastest_Image_Pattern_Matching\\Test Images\\Dst4.bmp",
                ImreadModes.Grayscale);
            Cv2.ImShow("src", src);
            Cv2.ImShow("dst", dst);
            Cv2.WaitKey(0);

            PatternMatch.PatternMatchByNCC patternMatch = new PatternMatch.PatternMatchByNCC();
            int pyramidMaxLayers = 3;
            int minReudceArea = 256; // 最小缩小面积
            bool maxLevelFirst = false; // 是否先缩小到最大层级
            patternMatch.LearnPattern(dst, pyramidMaxLayers, minReudceArea, maxLevelFirst, debug);

            patternMatch.ShowTemplatePyramid();

            // 匹配
            bool srcReverse = false; // 是否反转源图像
            double angleStep = 10; // 旋转角度步长
            bool autoAngleStep = false; // 是否自动计算角度步长
            double startAngle = 0;
            double angleRange = 360;
            double matchThreshold = 0.9; // 匹配阈值
            int maxMatchCount = 70; // 最大匹配数量
            bool useSIMD = true; // 是否使用SIMD优化
            double maxOverlap = 0; // 最大重叠比例
            bool subPixelEstimation = true; // 是否进行亚像素估计
            bool fastMode = false; // 是否使用快速模式
            // 匹配开始计时
            var stopwatch = Stopwatch.StartNew();
            patternMatch.Match(src,  srcReverse, angleStep, autoAngleStep, startAngle, angleRange,
                matchThreshold, maxMatchCount, useSIMD, maxOverlap, subPixelEstimation, fastMode, 10, debug);
            // 匹配结束计时
            stopwatch.Stop();
            // 匹配时间消耗
            Console.WriteLine($"匹配耗时: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            // 可视化结果
            Mat colorSrc = new Mat();
            Cv2.CvtColor(src, colorSrc, ColorConversionCodes.GRAY2BGR);
            patternMatch.Visualization(colorSrc, new Scalar(0, 255, 0), new Scalar(255, 0, 255),
                new Scalar(255, 0, 255), frontScale: 0.5, thickness: 1, showScore: false);
            
            Cv2.ImShow("Matched Result", colorSrc);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }
    }
}
