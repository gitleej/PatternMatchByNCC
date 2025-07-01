using OpenCvSharp;
using OpenCvSharp.Flann;
using OpenCvSharp.ML;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Point = OpenCvSharp.Point;

namespace PatternMatch
{
    [StructLayout(LayoutKind.Sequential)]
    struct CSingleTargetMatch
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]  // 4x2 点，展开成 10个double
        public double[] m_points;
        public double m_score;
        public double m_angle;
        public int index;
    }

    /// <summary>
    /// 匹配结果的单个目标。
    /// </summary>
    public class SingleTargetMatch
    {
        /// <summary>
        /// 匹配目标框的四个顶点
        /// </summary>
        public List<Point2d> Points { get; set; }
        /// <summary>
        /// 匹配目标的角度
        /// </summary>
        public double Angle { get; set; }
        /// <summary>
        /// 匹配目标的得分
        /// </summary>
        public double Score { get; set; }
        /// <summary>
        /// 匹配目标索引
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// 非托管类，用于模式匹配的归一化互相关（NCC）算法。
    /// </summary>
    public class CMatchPatternByNCC : IDisposable
    {
        private IntPtr _native;

        [DllImport("PatternMatchNative.dll", EntryPoint = "PM_Create", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr CPMCreate();

        [DllImport("PatternMatchNative.dll", EntryPoint = "PM_Destroy", CallingConvention = CallingConvention.Cdecl)]
        static extern void CPMDestroy(IntPtr native);

        [DllImport("PatternMatchNative.dll", EntryPoint = "Learn_Pattern", CallingConvention = CallingConvention.Cdecl)]
        static extern void CLearnPattern(IntPtr native,
            IntPtr pTemplImageData,
            int iTemplWidth,
            int iTemplHeight,
            int iTemplStride,
            int iPyramidLayers,
            int iMinReduceSize,
            [MarshalAs(UnmanagedType.I1)] 
            bool bAutoPyramidLayers);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Get_Template_Pyramid_Layers", CallingConvention = CallingConvention.Cdecl)]
        static extern int CGetTemplatePyramidLayers(IntPtr native);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Get_Template_Pyramid", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr CGetTemplatePyramid(IntPtr native, int index, out int rows, out int cols, out int type, out int step);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Match", CallingConvention = CallingConvention.Cdecl)]
        static extern int CMatch(
            IntPtr instance,
            IntPtr srcImageData,
            int srcImageWidth,
            int srcImageHeight,
            int srcImageStride,
            [Out] CSingleTargetMatch[] outArray,
            [MarshalAs(UnmanagedType.I1)] bool srcReverse,
            double angleStep,
            [MarshalAs(UnmanagedType.I1)] bool autoAngleStep,
            double startAngle,
            double angleRange,
            double matchThreshold,
            int maxMatchCount,
            [MarshalAs(UnmanagedType.I1)] bool useSIMD,
            double maxOverlap,
            [MarshalAs(UnmanagedType.I1)] bool subPixelEstimation,
            [MarshalAs(UnmanagedType.I1)] bool fastMode,
            [MarshalAs(UnmanagedType.I1)] bool debug);

        /// <summary>
        /// 匹配目标数量
        /// </summary>
        public int MatchedTargetNum { set; get; }

        /// <summary>
        /// 匹配结果列表
        /// </summary>
        public List<SingleTargetMatch> Matches { set; get; }

        public CMatchPatternByNCC()
        {
            _native = CPMCreate();
            if (_native == IntPtr.Zero)
            {
                throw new Exception("Failed to create native CMatchPatternByNCC instance.");
            }
        }

        public void Dispose()
        {
            if (_native != IntPtr.Zero)
            {
                CPMDestroy(_native);
                _native = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 学习模板图像金字塔
        /// </summary>
        /// <param name="templateImage">输入模板图像</param>
        /// <param name="pyramidLayers">图像金字塔层数</param>
        /// <param name="minReduceSize">最小图像缩放尺寸</param>
        /// <param name="autoPyramidLayers">是否自动计算图像金字塔层数</param>
        public void LearnPattern(Mat templateImage, int pyramidLayers, int minReduceSize, bool autoPyramidLayers)
        {
            unsafe
            {
                IntPtr pTemplateImageData =  (IntPtr)templateImage.Data;
                int templateWidth = templateImage.Width;
                int templateHeight = templateImage.Height;
                int templateStride = (int)templateImage.Step();
                if (_native == IntPtr.Zero)
                {
                    throw new ObjectDisposedException("CMatchPatternByNCC");
                }

                CLearnPattern(_native, pTemplateImageData, templateWidth, templateHeight, templateStride,
                    pyramidLayers, minReduceSize, autoPyramidLayers);
            }
        }

        /// <summary>
        /// 显示模板图像金字塔
        /// </summary>
        public void ShowTemplatePyramid()
        {
            int templatePyramidLayers = CGetTemplatePyramidLayers(_native);
            for (int i = 0; i < templatePyramidLayers; i++)
            {
                IntPtr ptr = CGetTemplatePyramid(_native, i, out int rows, out int cols, out int type, out int step);
                if (ptr == IntPtr.Zero)
                {
                    continue;
                }

                // Updated to use Mat.FromPixelData instead of the obsolete constructor
                Mat pyramidImage = Mat.FromPixelData(rows, cols, (MatType)type, ptr, (int)step);
                Cv2.ImShow($"pyramid:{i}", pyramidImage);
                Cv2.WaitKey(0);
            }

            Cv2.DestroyAllWindows();
        }

        public int Match(Mat src,
            bool srcReverse = false, double angleStep = 10, bool autoAngleStep = true,
            double startAngle = 0, double angleRange = 360, double matchThreshold = 0.9,
            int maxMatchCount = 70, bool useSIMD = true, double maxOverlap = 0,
            bool subPixelEstimation = true, bool fastMode = false, bool debug = false)
        {
            if (_native == IntPtr.Zero)
            {
                throw new ObjectDisposedException("CMatchPatternByNCC");
            }
            unsafe
            {
                IntPtr pSrcImageData = (IntPtr)src.Data;
                int srcWidth = src.Width;
                int srcHeight = src.Height;
                int srcStride = (int)src.Step();
                CSingleTargetMatch[] outArray = new CSingleTargetMatch[maxMatchCount];
                int matchCount = CMatch(_native, pSrcImageData, srcWidth, srcHeight, srcStride,
                    outArray, srcReverse, angleStep, autoAngleStep, startAngle, angleRange,
                    matchThreshold, maxMatchCount, useSIMD, maxOverlap, subPixelEstimation, fastMode, debug);
                Matches = new List<SingleTargetMatch>();
                for (int i = 0; i < matchCount; i++)
                {
                    var matchResult = outArray[i];
                    var points = new List<Point2d>
                    {
                        new Point2d(matchResult.m_points[0], matchResult.m_points[1]),
                        new Point2d(matchResult.m_points[2], matchResult.m_points[3]),
                        new Point2d(matchResult.m_points[4], matchResult.m_points[5]),
                        new Point2d(matchResult.m_points[6], matchResult.m_points[7])
                    };
                    Matches.Add(new SingleTargetMatch
                    {
                        Points = points,
                        Angle = matchResult.m_angle,
                        Score = matchResult.m_score,
                        Index = matchResult.index
                    });
                }

                MatchedTargetNum = matchCount;
                return matchCount;
            }
        }

        public void Visualization(
            Mat image, 
            Scalar color, 
            Scalar indexFrontColor = default,
            Scalar scoreFrontColor = default, 
            double frontScale = 0.5, 
            int thickness = 1,
            bool showIndex = true,
            bool showDirection = true,
            bool showMark = true, 
            bool showScore = true)
        {
            for (int i = 0; i < MatchedTargetNum; i++)
            {
                Point ptLT = ToPoint(Matches[i].Points[0]);
                Point ptRT = ToPoint(Matches[i].Points[1]);
                Point ptLB = ToPoint(Matches[i].Points[2]);
                Point ptRB = ToPoint(Matches[i].Points[3]);
                Point center = new Point((ptLT.X + ptLB.X + ptRB.X + ptRT.X) / 4.0f,
                    (ptLT.Y + ptLB.Y + ptRB.Y + ptRT.Y) / 4.0f);

                DrawDashLine(ref image, ptLT, ptRT, new Scalar(0, 0, 255), Scalar.All(0));
                DrawDashLine(ref image, ptRT, ptRB, new Scalar(0, 0, 255), Scalar.All(0));
                DrawDashLine(ref image, ptRB, ptLB, new Scalar(0, 0, 255), Scalar.All(0));
                DrawDashLine(ref image, ptLB, ptLT, new Scalar(0, 0, 255), Scalar.All(0));

                if (showIndex)
                {
                    // 添加索引标签
                    string index = $"{Matches[i].Index}";
                    Cv2.PutText(image, index, ptLT,
                        HersheyFonts.HersheySimplex, frontScale, indexFrontColor);
                }

                if (showDirection)
                {
                    // 绘制方向箭头，箭头位于中心线上
                    DrawDirectionArrow(image, color, thickness,
                        Math.Sqrt(Math.Pow(ptLB.X - ptLT.X, 2) + Math.Pow(ptLB.Y - ptLT.Y, 2)),
                        Math.Sqrt(Math.Pow(ptRT.X - ptLT.X, 2) + Math.Pow(ptRT.Y - ptLT.Y, 2)), center,
                        Matches[i].Angle);
                }

                if (showMark)
                {
                    // 绘制中心mark
                    DrawCrossMarker(ref image, center, 5, color, thickness);
                }

                if (showScore)
                {
                    // 添加得分标签
                    string label = $"{Matches[i].Score:F3}";
                    Cv2.PutText(image, label, center,
                        HersheyFonts.HersheySimplex, frontScale, scoreFrontColor);
                }
            }
        }

        /// <summary>
        /// 将Point2d转换为Point
        /// </summary>
        private Point ToPoint(Point2d p) => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y));

        /// <summary>
        /// 绘制虚线
        /// </summary>
        /// <param name="image">图像</param>
        /// <param name="ptStart">虚线起点</param>
        /// <param name="ptEnd">虚线终点</param>
        /// <param name="color1">颜色1</param>
        /// <param name="color2">颜色2</param>
        private void DrawDashLine(ref Mat image, Point ptStart, Point ptEnd, Scalar color1, Scalar color2)
        {
            // 使用LineIterator逐像素绘制虚线
            LineIterator itLine = new LineIterator(image, ptStart, ptEnd, PixelConnectivity.Connectivity8, false);
            int i = 0;
            foreach (var it in itLine)
            {
                Scalar color = ((i / 3) % 2 == 0) ? color1 : color2;
                //var color = (i % 3 == 0) ? color1 : color2;
                var pos = it.Pos;
                // 绘制像素点
                image.At<Vec3b>(pos.Y, pos.X) = new Vec3b((byte)color.Val0, (byte)color.Val1, (byte)color.Val2);
                i++;
            }
        }

        /// <summary>
        /// 绘制中心十字标记
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="center">中心点坐标</param>
        /// <param name="length">十字长度</param>
        /// <param name="color">标记颜色</param>
        /// <param name="thickness">标记粗细</param>
        private void DrawCrossMarker(ref Mat image, Point center, int length, Scalar color, int thickness)
        {
            Cv2.Line(image, new Point(center.X - length, center.Y), new Point(center.X + length, center.Y), color,
                thickness);
            Cv2.Line(image, new Point(center.X, center.Y - length), new Point(center.X, center.Y + length), color,
                thickness);
        }

        /// <summary>
        /// 绘制方向箭头
        /// </summary>
        private void DrawDirectionArrow(Mat image, Scalar color, int thickness, double width, double height,
            Point center, double angle)
        {
            // 计算箭头长度（使用目标宽度的一半）
            double arrowLength = Math.Max(height, width) * 0.5;

            // 计算箭头起点（中心点）
            Point2d startPoint = center;

            // 计算箭头终点
            double angleRad = angle * Math.PI / 180.0; // 转换为弧度
            startPoint = new Point2d(
                center.X - arrowLength * Math.Cos(angleRad),
                center.Y + arrowLength * Math.Sin(angleRad)
            );
            Point2d endPoint = new Point2d(
                center.X + arrowLength * Math.Cos(angleRad),
                center.Y - arrowLength * Math.Sin(angleRad)
            );

            // 计算箭头翼
            double arrowSize = width * 0.15; // 箭头翼的大小
            double arrowAngle = Math.PI / 6.0; // 箭头翼与主线的夹角（30度）

            // 计算箭头翼的两个端点
            Point2d wing1 = new Point2d(
                endPoint.X - arrowSize * Math.Cos(angleRad + arrowAngle),
                endPoint.Y + arrowSize * Math.Sin(angleRad + arrowAngle)
            );

            Point2d wing2 = new Point2d(
                endPoint.X - arrowSize * Math.Cos(angleRad - arrowAngle),
                endPoint.Y + arrowSize * Math.Sin(angleRad - arrowAngle)
            );

            // 绘制箭头主线
            Cv2.Line(image, ToPoint(startPoint), ToPoint(endPoint), color, thickness);

            // 绘制箭头翼
            Cv2.Line(image, ToPoint(endPoint), ToPoint(wing1), color, thickness);
            Cv2.Line(image, ToPoint(endPoint), ToPoint(wing2), color, thickness);
        }
    }
}
