using Google.Protobuf;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Templdata;
using static PatternMatch.BlockMax;

namespace PatternMatch
{
    /// <summary>
    /// Protobuf数据转换类
    /// </summary>
    public static class MatExtensions
    {
        /// <summary>
        /// 将连续存储的 Mat 像素数据拷贝到一个 byte 数组
        /// </summary>
        public static byte[] ToByteArray(this Mat mat)
        {
            if (!mat.IsContinuous())
            {
                mat = mat.Clone(); // 确保数据连续
            }

            int size = (int)(mat.Total() * mat.ElemSize());
            byte[] buffer = new byte[size];

            // DataPointer 是 IntPtr，可直接用于 Marshal.Copy
            Marshal.Copy(mat.Data, buffer, 0, size);

            return buffer;

        }

        /// <summary>
        /// 将 byte[] 数据拷回到一个已分配好尺寸和类型的 Mat
        /// </summary>
        public static void FromByteArray(this Mat mat, byte[] data)
        {
            if (!mat.IsContinuous())
                throw new InvalidOperationException("目标 Mat 必须是连续内存");

            int expected = (int)(mat.Total() * mat.ElemSize());
            if (data.Length != expected)
                throw new ArgumentException($"数据长度 ({data.Length}) 与 Mat 大小 ({expected}) 不匹配");

            Marshal.Copy(data, 0, mat.Data, data.Length);
        }
    }

    /// <summary>
    /// 模板数据类，包含模板图像金字塔及其统计参数
    /// </summary>
    public class TemplData
    {
        /// <summary>
        /// 图像金字塔列表，包含不同分辨率的模板图像
        /// </summary>
        public List<Mat> VecPyramid { get; set; } = new List<Mat>();
        /// <summary>
        /// 图像金字塔对应的模板均值列表
        /// </summary>
        public List<Scalar> VecTemplMean { get;  set; } = new List<Scalar>();
        /// <summary>
        /// 图像金字塔对应的模板归一化列表
        /// </summary>
        public List<double> VecTemplNorm { get;  set; } = new List<double>();
        /// <summary>
        /// 图像金字塔对应的逆面积列表，用于归一化匹配结果
        /// </summary>
        public List<double> VecInvArea { get;  set; } = new List<double>();
        /// <summary>
        /// 图像金字塔对应的匹配结果是否等于1的列表
        /// </summary>
        public List<bool> VecResultEqual1 { get;  set; } = new List<bool>();
        /// <summary>
        /// 图像金字塔是否已经学习
        /// </summary>
        public bool IsPatternLearned { get; set; } = false;
        /// <summary>
        /// 边界颜色
        /// </summary>
        public int BorderColor { get; set; }

        /// <summary>
        /// 清空模板图像金字塔数据
        /// </summary>
        public void Clear()
        {
            VecPyramid.Clear();
            VecTemplMean.Clear();
            VecTemplNorm.Clear();
            VecInvArea.Clear();
            VecResultEqual1.Clear();
        }

        /// <summary>
        /// 重置模板数据大小
        /// </summary>
        /// <param name="size">金字塔层数</param>
        public void Resize(int size)
        {
            ResizeList(VecTemplMean, size, default(Scalar));
            ResizeList(VecTemplNorm, size, 0.0);
            ResizeList(VecInvArea, size, 1.0);
            ResizeList(VecResultEqual1, size, false);
        }

        private void ResizeList<T>(List<T> list, int size, T defaultValue)
        {
            if (list.Count > size)
            {
                list.RemoveRange(size, list.Count - size);
            }
            else
            {
                while (list.Count < size)
                    list.Add(defaultValue);
            }
        }

        /// <summary>
        /// 保存模板数据到文件
        /// </summary>
        /// <param name="filePath">模板数据文件路径</param>
        /// <returns>是否保存成功，true成功，false失败。</returns>
        public bool Save(string filePath)
        {
            try
            {
                var proto = new TemplDataProto
                {
                    IsPatternLearned = IsPatternLearned,
                    BorderColor = BorderColor,
                    PyramidLayers = VecPyramid.Count
                };

                // 填充 pyramid
                foreach (var mat in VecPyramid)
                {
                    MatProto p = new MatProto();
                    p.Rows = mat.Rows;
                    p.Cols = mat.Cols;
                    p.Type = (int)mat.Type();
                    // 假设 Mat 数据通过 GetRawData() 返回 byte[]
                    p.Data = ByteString.CopyFrom(mat.ToByteArray());
                    proto.Pyramid.Add(p);
                }

                // templMean
                foreach (var sc in VecTemplMean)
                {
                    ScalarProto s = new ScalarProto();
                    s.V0 = sc.Val0;
                    s.V1 = sc.Val1;
                    s.V2 = sc.Val2;
                    s.V3 = sc.Val3;
                    proto.TemplMean.Add(s);
                }

                // templNorm
                proto.TemplNorm.Add(VecTemplNorm);

                // invArea & resultEqual1
                proto.InvArea.Add(VecInvArea);
                proto.ResultEqual1.Add(VecResultEqual1);

                // 写入文件
                var fs = File.Create(filePath);
                proto.WriteTo(fs);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 从模板数据文件中加载模板数据
        /// </summary>
        /// <param name="filePath">模板数据文件路径</param>
        /// <returns>是否加载成功，true成功，false失败。</returns>
        public bool Load(string filePath)
        {
            try
            {
                var proto = TemplDataProto.Parser.ParseFrom(File.OpenRead(filePath));
                Clear();

                // pyramid
                foreach (var p in proto.Pyramid)
                {
                    var m = new Mat(p.Rows, p.Cols, p.Type);
                    m.FromByteArray(p.Data.ToByteArray());
                    VecPyramid.Add(m);
                }

                // templMean
                VecTemplMean = proto.TemplMean
                    .Select(s => new Scalar(s.V0, s.V1, s.V2, s.V3))
                    .ToList();

                // templNorm
                VecTemplNorm = proto.TemplNorm.ToList();

                // invArea & resultEqual1
                VecInvArea = proto.InvArea.ToList();
                VecResultEqual1 = proto.ResultEqual1.ToList();

                IsPatternLearned = proto.IsPatternLearned;
                BorderColor = proto.BorderColor;

                // 保证其他列表长度与 pyramid 层数一致（可选）
                Resize(proto.Pyramid.Count);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 匹配结果类，包含匹配到的目标信息
    /// </summary>
    public class SingleMatchedTarget
    {
        /// <summary>
        /// 左上角坐标
        /// </summary>
        public Point2d LeftTop { get; set; }

        /// <summary>
        /// 左下角坐标
        /// </summary>
        public Point2d LeftBottom { get; set; }

        /// <summary>
        /// 右上角坐标
        /// </summary>
        public Point2d RightTop { get; set; }

        /// <summary>
        /// 右下角坐标
        /// </summary>
        public Point2d RightBottom { get; set; }

        /// <summary>
        /// 目标旋转角度（度）
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 匹配得分 (0-1)
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 目标索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 目标宽度
        /// </summary>
        public double Width => Math.Sqrt(Math.Pow(RightTop.X - LeftTop.X, 2) + Math.Pow(RightTop.Y - LeftTop.Y, 2));

        /// <summary>
        /// 目标高度
        /// </summary>
        public double Height => Math.Sqrt(Math.Pow(LeftBottom.X - LeftTop.X, 2) + Math.Pow(LeftBottom.Y - LeftTop.Y, 2));

        /// <summary>
        /// 目标中心点
        /// </summary>
        public Point2d Center { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public SingleMatchedTarget()
        {
            LeftTop = new Point2d();
            LeftBottom = new Point2d();
            RightTop = new Point2d();
            RightBottom = new Point2d();
            Angle = 0;
            Score = 0;
        }

        /// <summary>
        /// 在图像上绘制目标
        /// </summary>
        /// <param name="image">待绘制图像</param>
        /// <param name="color">绘制颜色</param>
        /// <param name="showMark"></param>
        /// <param name="showDirection"></param>
        /// <param name="showIndex"></param>
        /// <param name="scoreFrontColor">分数字体颜色</param>
        /// <param name="frontScale">字体大小</param>
        /// <param name="thickness">线条粗细</param>
        /// <param name="showScore"></param>
        /// <param name="indexFrontColor">索引字体颜色</param>
        public void Visualize(
            Mat image, 
            Scalar color,
            Scalar indexFrontColor,
            Scalar scoreFrontColor,
            double frontScale = 0.5,
            int thickness = 1, 
            bool showMark = true, 
            bool showDirection = true, 
            bool showIndex = true,
            bool showScore = false)
        {
            DrawDashLine(ref image, ToPoint(LeftTop), ToPoint(RightTop), new Scalar(0, 0, 255), Scalar.All(0));
            DrawDashLine(ref image, ToPoint(RightTop), ToPoint(RightBottom), new Scalar(0, 0, 255), Scalar.All(0));
            DrawDashLine(ref image, ToPoint(RightBottom), ToPoint(LeftBottom), new Scalar(0, 0, 255), Scalar.All(0));
            DrawDashLine(ref image, ToPoint(LeftBottom), ToPoint(LeftTop), new Scalar(0, 0, 255), Scalar.All(0));

            if (showIndex)
            {
                // 添加索引标签
                string index = $"{Index}";
                Cv2.PutText(image, index, ToPoint(LeftTop),
                    HersheyFonts.HersheySimplex, frontScale, indexFrontColor);
            }

            if (showDirection)
            {
                // 绘制方向箭头，箭头位于中心线上
                DrawDirectionArrow(image, color, thickness);
            }

            if (showMark)
            {
                // 绘制中心mark
                DrawCrossMarker(ref image, ToPoint(Center), 5, color, thickness);
            }

            if (showScore)
            {
                // 添加得分标签
                string label = $"{Score:F3}";
                Cv2.PutText(image, label, ToPoint(Center),
                    HersheyFonts.HersheySimplex, frontScale, scoreFrontColor);
            }
        }

        private void DrawCrossMarker(ref Mat image, Point center, int length, Scalar color, int thickness)
        {
            Cv2.Line(image, new Point(center.X - length, center.Y), new Point(center.X + length, center.Y), color,
                thickness);
            Cv2.Line(image, new Point(center.X, center.Y - length), new Point(center.X, center.Y + length), color,
                thickness);
        }

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
        /// 绘制方向箭头
        /// </summary>
        private void DrawDirectionArrow(Mat image, Scalar color, int thickness)
        {
            // 计算箭头长度（使用目标宽度的一半）
            double arrowLength = Math.Max(Height, Width) * 0.5;

            // 计算箭头起点（中心点）
            Point2d startPoint = Center;

            // 计算箭头终点
            double angleRad = Angle * Math.PI / 180.0; // 转换为弧度
            startPoint = new Point2d(
                Center.X - arrowLength * Math.Cos(angleRad),
                Center.Y + arrowLength * Math.Sin(angleRad)
            );
            Point2d endPoint = new Point2d(
                Center.X + arrowLength * Math.Cos(angleRad),
                Center.Y - arrowLength * Math.Sin(angleRad)
            );

            // 计算箭头翼
            double arrowSize = Width * 0.15; // 箭头翼的大小
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

        /// <summary>
        /// 将Point2d转换为Point
        /// </summary>
        private Point ToPoint(Point2d p) => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y));


        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return
                $"Target[Score={Score:F3}, Angle={Angle:F1}°, LeftTop=({LeftTop.X:F1}, {LeftTop.Y:F1}), RightTop=({RightTop.X:F1}, {RightTop.Y:F1}), RightBottom=({RightBottom.X:F1}, {RightBottom.Y:F1}), LeftBottom=({LeftBottom.X:F1}, {LeftBottom.Y:F1})]";
        }
    }

    /// <summary>
    /// 分块最大值处理类，用于将图像分块并计算每个块的最大值及其位置。
    /// </summary>
    public class BlockMax
    {
        /// <summary>
        /// 分块数据类，包含块的矩形区域、最大值及其位置。
        /// </summary>
        public class Block
        {
            /// <summary>
            /// 分块的矩形区域
            /// </summary>
            public Rect Rect { get; set; }
            /// <summary>
            /// 分块内的最大值
            /// </summary>
            public double DMax { get; set; }
            /// <summary>
            /// 分块内最大值的位置
            /// </summary>
            public Point PtMaxLoc { get; set; }

            /// <summary>
            /// 构造函数，初始化分块数据。
            /// </summary>
            public Block()
            {
                Rect = new Rect();
                DMax = 0.0;
                PtMaxLoc = new Point();
            }
            /// <summary>
            /// 构造函数，使用指定的矩形区域、最大值和位置初始化分块数据。
            /// </summary>
            /// <param name="rect">分块矩形区域</param>
            /// <param name="dMax">最大值</param>
            /// <param name="ptMaxLoc">最大值位置</param>
            public Block(Rect rect, double dMax, Point ptMaxLoc)
            {
                Rect = rect;
                DMax = dMax;
                PtMaxLoc = ptMaxLoc;
            }
        }

        /// <summary>
        /// 分块列表，包含所有分块的最大值和位置。
        /// </summary>
        public List<Block> VecBlock { get; set; }
        /// <summary>
        /// 原始图像
        /// </summary>
        public Mat MatSrc { get; set; }

        /// <summary>
        /// 构造函数，初始化分块列表和原始图像。
        /// </summary>
        public BlockMax()
        {
            VecBlock = new List<Block>();
            MatSrc = new Mat();
        }

        /// <summary>
        /// 构造并分块处理。将matSrc拆成若干Block，分别计算每个Block的最大值及其位置。
        /// </summary>
        /// <param name="matSrc">输入源图像，保留引用</param>
        /// <param name="sizeTemplate">用于决定块大小，一般为模板尺寸</param>
        public BlockMax(Mat matSrc, Size sizeTemplate)
        {
            // 输入验证
            if (matSrc.Empty())
                throw new ArgumentException("输入图像不能为空");
            
            MatSrc = matSrc.Clone();

            // 将MatSrc拆分成若干个Block，分别计算每个Block的最大值及其位置
            int iBlockWidth = sizeTemplate.Width * 2;
            int iBlockHeight = sizeTemplate.Height * 2;

            if (iBlockWidth <= 0 || iBlockHeight <= 0)
            {
                VecBlock = new List<Block>();
                return;
            }

            int iCol = MatSrc.Cols / iBlockWidth;
            bool bHResidue = (MatSrc.Cols % iBlockWidth) != 0;

            int iRow = MatSrc.Rows / iBlockHeight;
            bool bVResidue = (MatSrc.Rows % iBlockHeight) != 0;

            // 如果没有完整块，则直接保留空列表
            if (iCol == 0 || iRow == 0)
            {
                VecBlock = new List<Block>();
                return;
            }

            //VecBlock = new List<Block>(iCol * iRow);
            // 遍历完整块
            //int iCount = 0;
            for (int y = 0; y < iRow; y++)
            {
                for (int x = 0; x < iCol; x++)
                {
                    Rect rectBlock = new Rect(
                        x * iBlockWidth,
                        y * iBlockHeight,
                        iBlockWidth,
                        iBlockHeight
                    );

                    // 限制 rect 在图像范围内（正常情况下 iCol、iRow 是整除后计算，rectBlock 应该都在范围内）
                    rectBlock = ClipRectToMat(MatSrc, rectBlock);

                    // 取 sub-mat，计算 minMaxLoc
                    using (Mat roi = new Mat(MatSrc, rectBlock))
                    {
                        Cv2.MinMaxLoc(roi, out _, out double maxVal, out _, out Point maxLocInRoi);
                        // 将局部坐标转换到原图坐标
                        Point absPt = new Point(maxLocInRoi.X + rectBlock.X, maxLocInRoi.Y + rectBlock.Y);
                        VecBlock.Add(new Block(rectBlock, maxVal, absPt));
                    }
                }
            }

            // 处理残余情况：水平和/或垂直残余区域
            if (bHResidue && bVResidue)
            {
                // 右侧整列剩余（完整高度）
                {
                    Rect rectRight = new Rect(iCol * iBlockWidth, 0, MatSrc.Cols - iCol * iBlockWidth, MatSrc.Rows);
                    rectRight = ClipRectToMat(MatSrc, rectRight);
                    using (Mat roi = new Mat(MatSrc, rectRight))
                    {
                        Cv2.MinMaxLoc(roi, out _, out double maxVal, out _, out Point maxLocInRoi);
                        Point absPt = new Point(maxLocInRoi.X + rectRight.X, maxLocInRoi.Y + rectRight.Y);
                        VecBlock.Add(new Block(rectRight, maxVal, absPt));
                    }
                }
                // 底部完整宽度剩余
                {
                    Rect rectBottom = new Rect(0, iRow * iBlockHeight, iCol * iBlockWidth, MatSrc.Rows - iRow * iBlockHeight);
                    rectBottom = ClipRectToMat(MatSrc, rectBottom);
                    using (Mat roi = new Mat(MatSrc, rectBottom))
                    {
                        Cv2.MinMaxLoc(roi, out _, out double maxVal, out _, out Point maxLocInRoi);
                        Point absPt = new Point(maxLocInRoi.X + rectBottom.X, maxLocInRoi.Y + rectBottom.Y);
                        VecBlock.Add(new Block(rectBottom, maxVal, absPt));
                    }
                }
            }
            else if (bHResidue)
            {
                // 右侧整列剩余（完整高度）
                Rect rectRight = new Rect(iCol * iBlockWidth, 0, MatSrc.Cols - iCol * iBlockWidth, MatSrc.Rows);
                rectRight = ClipRectToMat(MatSrc, rectRight);
                using (Mat roi = new Mat(MatSrc, rectRight))
                {
                    Cv2.MinMaxLoc(roi, out _, out double maxVal, out _, out Point maxLocInRoi);
                    Point absPt = new Point(maxLocInRoi.X + rectRight.X, maxLocInRoi.Y + rectRight.Y);
                    VecBlock.Add(new Block(rectRight, maxVal, absPt));
                }
            }
            else if (bVResidue)
            {
                // 底部完整宽度剩余
                Rect rectBottom = new Rect(0, iRow * iBlockHeight, iCol * iBlockWidth, MatSrc.Rows - iRow * iBlockHeight);
                rectBottom = ClipRectToMat(MatSrc, rectBottom);
                using (Mat roi = new Mat(MatSrc, rectBottom))
                {
                    Cv2.MinMaxLoc(roi, out _, out double maxVal, out _, out Point maxLocInRoi);
                    Point absPt = new Point(maxLocInRoi.X + rectBottom.X, maxLocInRoi.Y + rectBottom.Y);
                    VecBlock.Add(new Block(rectBottom, maxVal, absPt));
                }
            }
        }

        /// <summary>
        /// 更新与 rectIgnore 有交集的区块，重新计算该区块的最大值和位置
        /// </summary>
        /// <param name="rectIgnore">忽略区域（在原图坐标系中），凡与之相交的区块重新 minMaxLoc</param>
        public void UpdateMax(Rect rectIgnore)
        {
            if (MatSrc == null || VecBlock == null || VecBlock.Count == 0)
                return;

            // 对每个 block，检测是否与 rectIgnore 相交
            for (int i = 0; i < VecBlock.Count; i++)
            {
                Rect blockRect = VecBlock[i].Rect;
                Rect inter = IntersectionRect(blockRect, rectIgnore);
                if (inter.Width <= 0 || inter.Height <= 0)
                {
                    // 无交集，跳过
                    continue;
                }
                // 有交集，重新对整个 blockRect 区域做 minMaxLoc 更新
                Rect rectBlock = blockRect;
                rectBlock = ClipRectToMat(MatSrc, rectBlock);
                using (Mat roi = new Mat(MatSrc, rectBlock))
                {
                    Cv2.MinMaxLoc(roi, out _, out double maxVal, out _, out Point maxLocInRoi);
                    Point absPt = new Point(maxLocInRoi.X + rectBlock.X, maxLocInRoi.Y + rectBlock.Y);
                    VecBlock[i].DMax = maxVal;
                    VecBlock[i].PtMaxLoc = absPt;
                }
            }
        }

        /// <summary>
        /// 从当前所有 block 中找出最大值和位置。如果 vecBlock 为空，则全图 minMaxLoc。
        /// </summary>
        /// <param name="dMax">输出最大值</param>
        /// <param name="ptMaxLoc">输出最大值位置</param>
        public void GetMaxValueLoc(out double dMax, out Point ptMaxLoc)
        {
            if (MatSrc == null)
            {
                dMax = 0;
                ptMaxLoc = new Point(0, 0);
                return;
            }

            if (VecBlock == null || VecBlock.Count == 0)
            {
                // 整图 minMaxLoc
                Cv2.MinMaxLoc(MatSrc, out _, out double maxVal, out _, out Point maxLoc);
                dMax = maxVal;
                ptMaxLoc = maxLoc;
                return;
            }

            // 在 block 列表中找最大值
            int idx = 0;
            dMax = VecBlock[0].DMax;
            for (int i = 1; i < VecBlock.Count; i++)
            {
                if (VecBlock[i].DMax >= dMax)
                {
                    idx = i;
                    dMax = VecBlock[i].DMax;
                }
            }
            ptMaxLoc = VecBlock[idx].PtMaxLoc;
        }

        /// <summary>
        /// 辅助：将 rect 限制到 matSrc 大小内
        /// </summary>
        private static Rect ClipRectToMat(Mat mat, Rect rect)
        {
            int x = Math.Max(rect.X, 0);
            int y = Math.Max(rect.Y, 0);
            int w = rect.Width;
            int h = rect.Height;
            if (x + w > mat.Cols)
                w = mat.Cols - x;
            if (y + h > mat.Rows)
                h = mat.Rows - y;
            if (w < 0) w = 0;
            if (h < 0) h = 0;
            return new Rect(x, y, w, h);
        }

        /// <summary>
        /// 辅助：计算两个 Rect 的交集区域
        /// </summary>
        private static Rect IntersectionRect(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            int w = x2 - x1;
            int h = y2 - y1;
            if (w <= 0 || h <= 0)
                return new Rect(0, 0, 0, 0);
            return new Rect(x1, y1, w, h);
        }
    }

    /// <summary>
    /// Represents the parameters and results of a matching operation, including positional, angular, and scoring data.
    /// </summary>
    /// <remarks>This class encapsulates various properties related to a matching operation, such as the
    /// position, score, angle,  and bounding regions. It also includes additional metadata like whether the match is on
    /// a border or marked for deletion. Instances of this class can be initialized with default values or specific
    /// parameters for position, score, and angle.</remarks>
    public class MatchParameter
    {
        /// <summary>
        /// 匹配框左上角点坐标
        /// </summary>
        public Point2d Pt { get; set; }
        /// <summary>
        /// 匹配得分
        /// </summary>
        public double DMatchScore { get; set; }
        /// <summary>
        /// 匹配角度（度）
        /// </summary>
        public double DMatchAngle { get; set; }
        /// <summary>
        /// 矩形区域（ROI），用于匹配时的搜索范围
        /// </summary>
        public Rect RectRoi { get; set; }
        /// <summary>
        /// 匹配起始角度
        /// </summary>
        public double DAngleStart { get; set; }
        /// <summary>
        /// 匹配结束角度
        /// </summary>
        public double DAngleEnd { get; set; }
        /// <summary>
        /// 旋转矩形区域
        /// </summary>
        public RotatedRect RectR { get; set; }
        /// <summary>
        /// 边界矩形区域
        /// </summary>
        public Rect RectBounding { get; set; }
        /// <summary>
        /// 是否删除该匹配结果
        /// </summary>
        public bool BDelete { get; set; }
        /// <summary>
        /// 匹配结果列表
        /// </summary>
        public double[,] VecResult { get; set; }
        /// <summary>
        /// 最大得分索引
        /// </summary>
        public int IMaxScoreIndex { get; set; }
        /// <summary>
        /// 边界位置标志，指示匹配位置是否在图像边界上
        /// </summary>
        public bool BPosOnBorder { get; set; }
        /// <summary>
        /// 亚像素坐标
        /// </summary>
        public Point2d PtSubPixel  { get; set; }
        /// <summary>
        /// 新匹配角度（度）
        /// </summary>
        public double DNewAngle { get; set; }

        /// <summary>
        /// 构造函数，使用指定的参数初始化匹配参数。
        /// </summary>
        /// <param name="ptMinMax">最大最小值点坐标</param>
        /// <param name="dScore">得分</param>
        /// <param name="dAngle">角度</param>
        public MatchParameter(Point2d ptMinMax, double dScore, double dAngle)
        {
            Pt = ptMinMax;
            DMatchScore = dScore;
            DMatchAngle = dAngle;
            RectRoi = new Rect();
            DAngleStart = 0.0;
            DAngleEnd = 0.0;
            RectR = new RotatedRect();
            RectBounding = new Rect();
            BDelete = false;
            VecResult = new double[3, 3];
            IMaxScoreIndex = -1;
            BPosOnBorder = false;
            PtSubPixel = new Point2d();
            DNewAngle = 0.0;
        }

        /// <summary>
        /// 构造函数，初始化匹配参数的默认值。
        /// </summary>
        public MatchParameter()
        {
            Pt = new Point2d();
            DMatchScore = 0.0;
            DMatchAngle = 0.0;
            RectRoi = new Rect();
            DAngleStart = 0.0;
            DAngleEnd = 0.0;
            RectR = new RotatedRect();
            RectBounding = new Rect();
            BDelete = false; 
            VecResult = new double[3, 3];
            IMaxScoreIndex = -1;
            BPosOnBorder = false;
            PtSubPixel = new Point2d();
            DNewAngle = 0.0;
        }
    }
}
