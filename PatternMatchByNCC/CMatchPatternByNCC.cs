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
using PatternMatchByNCC;
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

    internal static class NativeMethods
    {
        [DllImport("PatternMatchNative.dll", EntryPoint = "PM_Create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CPMCreate();

        [DllImport("PatternMatchNative.dll", EntryPoint = "PM_Destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CPMDestroy(IntPtr native);

        [DllImport("PatternMatchNative.dll", EntryPoint = "Learn_Pattern", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CLearnPattern(IntPtr native,
            IntPtr pTemplImageData,
            int iTemplWidth,
            int iTemplHeight,
            int iTemplStride,
            int iPyramidLayers,
            int iMinReduceSize,
            [MarshalAs(UnmanagedType.I1)]
            bool bAutoPyramidLayers,
            [MarshalAs(UnmanagedType.I1)]
            bool bDebug);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Get_Template_Pyramid_Layers", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CGetTemplatePyramidLayers(IntPtr native);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Get_Template_Pyramid", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CGetTemplatePyramid(IntPtr native, int index, out int rows, out int cols, out int type, out int step);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Match", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CMatch(
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
            int numWorks,
            [MarshalAs(UnmanagedType.I1)] bool debug);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Save_Template_Data", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CSaveTemplateData(IntPtr native, string fileName);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Load_Template_Data", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool CLoadTemplateData(IntPtr native, string fileName);
    }

    /// <summary>
    /// 非托管类，用于模式匹配的归一化互相关（NCC）算法。
    /// </summary>
    public class CMatchPatternByNCC : IDisposable, IPatternMatchByNCC
    {
        private IntPtr _native;
        
        /// <summary>
        /// 匹配目标数量
        /// </summary>
        public int MatchedTargetNum { set; get; }

        /// <summary>
        /// 匹配结果列表
        /// </summary>
        public List<SingleTargetMatch> Matches { set; get; }

        /// <summary>
        /// 构造函数，创建一个新的CMatchPatternByNCC实例。
        /// </summary>
        /// <exception cref="Exception">Failed to create native CMatchPatternByNCC instance.</exception>
        public CMatchPatternByNCC()
        {
            _native = NativeMethods.CPMCreate();
            if (_native == IntPtr.Zero)
            {
                throw new Exception("Failed to create native CMatchPatternByNCC instance.");
            }
        }

        /// <summary>
        /// 析构函数，释放CMatchPatternByNCC实例的资源。
        /// </summary>
        public void Dispose()
        {
            if (_native != IntPtr.Zero)
            {
                NativeMethods.CPMDestroy(_native);
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
        /// <param name="debug">是否启用Debug模式</param>
        public void LearnPattern(Mat templateImage, int pyramidLayers, int minReduceSize, bool autoPyramidLayers, bool debug = false)
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

                NativeMethods.CLearnPattern(_native, pTemplateImageData, templateWidth, templateHeight, templateStride,
                    pyramidLayers, minReduceSize, autoPyramidLayers, debug);
            }
        }

        /// <summary>
        /// 显示模板图像金字塔
        /// </summary>
        public void ShowTemplatePyramid()
        {
            int templatePyramidLayers = NativeMethods.CGetTemplatePyramidLayers(_native);
            Console.WriteLine($"金字塔层数: {templatePyramidLayers}");
            for (int i = 0; i < templatePyramidLayers; i++)
            {
                IntPtr ptr = NativeMethods.CGetTemplatePyramid(_native, i, out int rows, out int cols, out int type, out int step);
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

        /// <summary>
        /// 模板匹配方法，使用归一化互相关(NCC)进行匹配。
        /// </summary>
        /// <param name="src">输入源图像</param>
        /// <param name="srcReverse">是否进行源图像反转</param>
        /// <param name="angleStep">旋转角度步长</param>
        /// <param name="autoAngleStep">是否自动计算旋转角度步长</param>
        /// <param name="startAngle">起始角度</param>
        /// <param name="angleRange">角度范围</param>
        /// <param name="matchThreshold">匹配得分阈值</param>
        /// <param name="maxMatchCount">最大匹配数量</param>
        /// <param name="useSIMD">是否启用SIMD加速</param>
        /// <param name="maxOverlap">最大重叠率</param>
        /// <param name="subPixelEstimation">是否启用亚像素估计</param>
        /// <param name="fastMode">是否启用快速匹配模式</param>
        /// <param name="numWorks">线程数量，默认为0。</param>
        /// <param name="debug">是否启用调试模式</param>
        /// <returns>实际匹配目标数量</returns>
        /// <exception cref="ObjectDisposedException">非托管类指针为空</exception>
        public int Match(Mat src,
            bool srcReverse = false, double angleStep = 10, bool autoAngleStep = true,
            double startAngle = 0, double angleRange = 360, double matchThreshold = 0.9,
            int maxMatchCount = 70, bool useSIMD = true, double maxOverlap = 0,
            bool subPixelEstimation = true, bool fastMode = false, int numWorks=0, bool debug = false)
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
                int matchCount = NativeMethods.CMatch(_native, pSrcImageData, srcWidth, srcHeight, srcStride,
                    outArray, srcReverse, angleStep, autoAngleStep, startAngle, angleRange,
                    matchThreshold, maxMatchCount, useSIMD, maxOverlap, subPixelEstimation, fastMode, numWorks, debug);
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

        /// <summary>
        /// 匹配目标可视化
        /// </summary>
        /// <param name="image">输入可视化彩色图像</param>
        /// <param name="color">匹配框颜色</param>
        /// <param name="indexFrontColor">索引值标签颜色</param>
        /// <param name="scoreFrontColor">得分标签颜色</param>
        /// <param name="frontScale">字体大小</param>
        /// <param name="thickness">线条粗细</param>
        /// <param name="showIndex">是否显示索引值标签</param>
        /// <param name="showDirection">是否显示目标方向箭头</param>
        /// <param name="showMark">是否显示匹配中心标记</param>
        /// <param name="showScore">是否显示得分标签</param>
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
        /// 保存模板数据到文件
        /// </summary>
        /// <param name="filePath">模板数据文件路径</param>
        /// <returns>是否保存成功，true成功，false失败</returns>
        public bool SaveTemplateData(string filePath)
        {
            return NativeMethods.CSaveTemplateData(_native, filePath);
        }

        /// <summary>
        /// 从文件加载模板数据
        /// </summary>
        /// <param name="filePath">模板数据文件路径</param>
        /// <returns>是否加载成功，true成功，false失败</returns>
        public bool LoadTemplateData(string filePath)
        {
            return NativeMethods.CLoadTemplateData(_native, filePath);
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
            double arrowLength = Math.Min(height, width) * 0.5;

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
            double arrowSize = Math.Min(height, width) * 0.15; // 箭头翼的大小
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
