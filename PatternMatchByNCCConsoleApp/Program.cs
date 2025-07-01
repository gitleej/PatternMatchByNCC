using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using PatternMatch;

namespace PatternMatchByNCC
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool debug = false; // 是否开启调试模式
            int repeatTimes = 100; // 重复匹配次数

            // 加载源图像和目标图像
            Mat src = Cv2.ImRead("F:\\02-Code\\Fastest_Image_Pattern_Matching\\Test Images\\Src4.bmp",
                ImreadModes.Grayscale);
            Mat dst = Cv2.ImRead("F:\\02-Code\\Fastest_Image_Pattern_Matching\\Test Images\\Dst4.bmp",
                ImreadModes.Grayscale);
            Cv2.ImShow("src", src);
            Cv2.ImShow("dst", dst);
            Cv2.WaitKey(0);

            // 测试CMatchPatternByNCC类
            CMatchPatternByNCC matchPatternByNCC = new CMatchPatternByNCC();
            // 学习模板图像金字塔
            int pyramidLayers = 3; // 图像金字塔层数
            int minReduceSize = 256; // 最小图像缩放尺寸
            bool autoPyramidLayers = true; // 是否自动计算图像金字塔层数
            matchPatternByNCC.LearnPattern(dst, pyramidLayers, minReduceSize, autoPyramidLayers);
            // 显示模板图像金字塔
            matchPatternByNCC.ShowTemplatePyramid();

            // 匹配
            // 匹配开始计时
            var stopwatch = Stopwatch.StartNew();
            int matchedTargetNum = 0;
            for (int i = 0; i < repeatTimes; i++)
            {
                var repeatStopWatch = Stopwatch.StartNew();
                matchedTargetNum = matchPatternByNCC.Match(src,
                    srcReverse: false, angleStep: 10, autoAngleStep: true, startAngle: 0, angleRange: 180,
                    matchThreshold: 0.9, maxMatchCount: 70, useSIMD: true, maxOverlap: 0,
                    subPixelEstimation: true, fastMode: false, debug:false);
                repeatStopWatch.Stop();
                Console.WriteLine($"第 {i + 1} 次匹配耗时: {repeatStopWatch.Elapsed.TotalMilliseconds:F2} ms");
            }
            // 匹配结束计时
            stopwatch.Stop();
            // 匹配时间消耗
            Console.WriteLine($"匹配耗时: {stopwatch.Elapsed.TotalMilliseconds / repeatTimes:F2} ms");
            Mat showMat = new Mat();
            // 可视化匹配结果
            // 输出匹配结果
            for (int i = 0; i < matchPatternByNCC.MatchedTargetNum; i++)
            {
                Console.WriteLine($"匹配结果 {i + 1}:");
                //Console.WriteLine($"位置: {ptLT}, {ptRB}, {ptLB}, {ptRT}");
                Console.WriteLine($"角度: {matchPatternByNCC.Matches[i].Angle}");
                Console.WriteLine($"匹配分数: {matchPatternByNCC.Matches[i].Score}");
            }
            Cv2.CvtColor(src, showMat, ColorConversionCodes.GRAY2BGR);
            matchPatternByNCC.Visualization(showMat, new Scalar(0, 255, 0), new Scalar(255, 0, 255),
                new Scalar(255, 0, 255), frontScale: 0.5, thickness: 1, showScore:false);
            Cv2.ImShow($"{matchedTargetNum}", showMat);
            Cv2.WaitKey(0);

            PatternMatch.PatternMatchByNCC patternMatch = new PatternMatch.PatternMatchByNCC();
            int pyramidMaxLayers = 3;
            int minReudceArea = 256; // 最小缩小面积
            bool maxLevelFirst = false; // 是否先缩小到最大层级
            // TemplData templData = patternMatch.LearnPattern(dst, pyramidMaxLayers, minReudceArea, maxLevelFirst, debug);
            patternMatch.LearnPattern(dst, pyramidMaxLayers,
                minReudceArea, maxLevelFirst, debug);
            // 显示模板图像金字塔
            patternMatch.ShowTemplatePyramid();

            // 匹配
            bool srcReverse = false; // 是否反转源图像
            double angleStep = 10; // 旋转角度步长
            bool autoAngleStep = true; // 是否自动计算角度步长
            double startAngle = 0;
            double angleRange = 180;
            double matchThreshold = 0.9; // 匹配阈值
            int maxMatchCount = 70; // 最大匹配数量
            bool useSIMD = true; // 是否使用SIMD优化
            double maxOverlap = 0; // 最大重叠比例
            bool subPixelEstimation = true; // 是否进行亚像素估计
            bool fastMode = false; // 是否使用快速模式
            // 匹配开始计时
            List<SingleMatchedTarget> result = new List<SingleMatchedTarget>();
            stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < repeatTimes; i++)
            {
                var repeatStopWatch = Stopwatch.StartNew();
                patternMatch.Match(src,  srcReverse, angleStep, autoAngleStep, startAngle, angleRange,
                    matchThreshold, maxMatchCount, useSIMD, maxOverlap, subPixelEstimation, fastMode, debug);
                repeatStopWatch.Stop();
                Console.WriteLine($"第 {i + 1} 次匹配耗时: {repeatStopWatch.Elapsed.TotalMilliseconds:F2} ms");
            }
            // 匹配结束计时
            stopwatch.Stop();
            // 匹配时间消耗
            Console.WriteLine($"匹配耗时: {stopwatch.Elapsed.TotalMilliseconds / repeatTimes:F2} ms");
            // 可视化结果
            // 输出匹配结果
            for (int i = 0; i < patternMatch.MatchedTargetNum; i++)
            {
                Console.WriteLine($"匹配结果 {i + 1}:");
                //Console.WriteLine($"位置: {ptLT}, {ptRB}, {ptLB}, {ptRT}");
                Console.WriteLine($"角度: {patternMatch.Matches[i].Angle}");
                Console.WriteLine($"匹配分数: {patternMatch.Matches[i].Score}");
            }
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
