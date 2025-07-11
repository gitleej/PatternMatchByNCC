using OpenCvSharp;
using OpenCvSharp.Internal.Vectors;
using SIMDConv;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PatternMatchByNCC;

namespace PatternMatch
{
    /// <summary>
    /// 模板匹配类，使用归一化互相关(NCC)进行匹配
    /// </summary>
    public class PatternMatchByNCC : IPatternMatchByNCC
    {
        private const int MatchCandidateNum = 5;
        /// <summary>
        /// 模板数据
        /// </summary>
        public TemplData TemplateData { get; set; }
        /// <summary>
        /// 匹配到的目标数量
        /// </summary>
        public int MatchedTargetNum { get; set; } = 0;
        /// <summary>
        /// 匹配到的目标列表
        /// </summary>
        public List<SingleMatchedTarget> Matches { get; set; }

        /// <summary>
        /// 构造方法
        /// </summary>
        public PatternMatchByNCC()
        {
            TemplateData = new TemplData();
            Matches = new List<SingleMatchedTarget>();
            MatchedTargetNum = 0;
        }

        /// <summary>
        /// 学习模板图像并构建金字塔
        /// </summary>
        /// <param name="matTemp">输入模板图像</param>
        /// <param name="pyramidMaxLayers">图像金字塔层数</param>
        /// <param name="minReudceArea">最小缩放尺寸</param>
        /// <param name="maxLevelFirst">是否优先使用给定的图像金字塔层数。
        /// true使用给定的图像金字塔层数，
        /// false使用最小缩放尺寸自动计算图像金字塔层数</param>
        /// <param name="debug">是否启动debug。</param>
        public void LearnPattern(
            Mat matTemp,
            int pyramidMaxLayers,
            int minReudceArea,
            bool maxLevelFirst,
            bool debug = false)
        {
            // 输入验证
            if (matTemp == null)
            {
                throw new ArgumentNullException(nameof(matTemp), "输入图像不能为空");
            }
            if (pyramidMaxLayers < 1 && maxLevelFirst)
            {
                throw new ArgumentException("金字塔层数必须大于0", nameof(pyramidMaxLayers));
            }
            if (minReudceArea < 1 && !maxLevelFirst)
            {
                throw new ArgumentException("最小缩放尺寸必须大于0", nameof(minReudceArea));
            }

            TemplateData = new TemplData();
            if (!maxLevelFirst)
            {
                pyramidMaxLayers = GetTopLayer(matTemp, minReudceArea);
            }
            else
            {
                pyramidMaxLayers -= 1;
            }

            // 构建模板图像金字塔
            VectorOfMat pyramidArray = new VectorOfMat();
            Cv2.BuildPyramid(matTemp, pyramidArray, pyramidMaxLayers);
            TemplateData.VecPyramid = pyramidArray.ToArray().ToList();

            if (debug)
            {
                foreach (var mat in TemplateData.VecPyramid)
                {
                    Cv2.ImShow("pyramid", mat);
                    Cv2.WaitKey(0);
                }
                Cv2.DestroyAllWindows();
            }

            // 计算模板边界颜色(用于仿射变换填充)
            TemplateData.BorderColor = Cv2.Mean(matTemp).Val0 < 128 ? 0 : 255; // 假设边界颜色为黑色或白色
            TemplateData.Resize(TemplateData.VecPyramid.Count); // 确保所有列表大小一致

            // 计算每层金字塔图像的统计参数
            for (int i = 0; i < TemplateData.VecPyramid.Count; i++)
            {
                Mat matPyramid = TemplateData.VecPyramid[i];

                // 计算每层图像的逆面积
                double invArea = 1.0 / (matPyramid.Width * matPyramid.Height);

                // 计算每层图像的均值和标准差
                Scalar templMean, templSdv;
                Cv2.MeanStdDev(matPyramid, out templMean, out templSdv);
                double templNorm = templSdv[0] * templSdv[0] + templSdv[1] * templSdv[1] + templSdv[2] * templSdv[2];

                // 检查是否为纯色图像
                if (templNorm < double.Epsilon) // 如果标准差接近0，认为是纯色图像
                {
                    TemplateData.VecResultEqual1[i] = true; // 标记为纯色图像
                }

                // 计算总平方和
                double templSum = templNorm + templMean[0] * templMean[0] + templMean[1] * templMean[1] +
                                  templMean[2] * templMean[2];
                templSum /= invArea;
                templNorm = Math.Sqrt(templNorm);
                templNorm /= Math.Sqrt(invArea);

                // 保存计算结果
                TemplateData.VecTemplMean[i] = templMean;
                TemplateData.VecTemplNorm[i] = templNorm;
                TemplateData.VecInvArea[i] = invArea;
            }

            TemplateData.IsPatternLearned = true;
        }

        /// <summary>
        /// 根据最小缩小面积计算金字塔顶层数量
        /// </summary>
        /// <param name="mat">输入图像</param>
        /// <param name="minReudceArea">最小缩放尺寸</param>
        /// <returns>金字塔顶层数量</returns>
        private int GetTopLayer(Mat mat, int minReudceArea)
        {
            int topLayers = 0;
            int minudceArea = (int)Math.Pow(Math.Sqrt(minReudceArea), 2);
            int area = mat.Width * mat.Height;

            while (area > minudceArea)
            {
                area /= 4; // 每次缩小为原来的1/4
                topLayers++;
            }

            return topLayers;
        }

        /// <summary>
        /// 模板匹配方法，使用归一化互相关(NCC)进行匹配
        /// </summary>
        /// <param name="src">输入源图像</param>
        /// <param name="reverse">输入图像反转</param>
        /// <param name="angleStep">角度步长</param>
        /// <param name="autoAngleStep">是否使用自动计算角度步长</param>
        /// <param name="startAngle">起始角度</param>
        /// <param name="angleRange">角度范围</param>
        /// <param name="scoreThreshold">匹配得分阈值</param>
        /// <param name="maxMatchCount">最大匹配数量</param>
        /// <param name="useSIMD">是否使用SIMD优化</param>
        /// <param name="maxOverlap">最大重叠比率</param>
        /// <param name="subPixelEstimation">是否进行亚像素估计</param>
        /// <param name="fastMode">是否使用快速模式，启用则进行粗匹配，提升速度，牺牲精度</param>
        /// <param name="debug">是否启用调试</param>
        /// <returns>匹配结果</returns>
        public int Match(
            Mat src, 
            // TemplData TemplateData, 
            bool reverse, 
            double angleStep = 5,
            bool autoAngleStep = false,
            double startAngle = 0,
            double angleRange = 360,
            double scoreThreshold = 0.8, // 匹配分数阈值
            int maxMatchCount = 100, // 最大匹配数量
            bool useSIMD = true, // 是否使用SIMD优化
            double maxOverlap = 0.5, // 最大重叠比例
            bool subPixelEstimation = false, // 是否进行亚像素估计
            bool fastMode = false, // 是否使用快速模式
            int numWorks = 0,
            bool debug=false)
        {
            // 输入验证
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src), "源图像不能为空");
            }
            if (TemplateData == null || !TemplateData.IsPatternLearned || TemplateData.VecPyramid.Count == 0)
            {
                throw new ArgumentException("模板数据无效或未学习", nameof(TemplateData));
            }
            // 获取模板图像（第0层为原始尺寸）
            Mat templateImg = TemplateData.VecPyramid[0];

            // 验证源图像和模板图像是否为空
            if (src.Empty() || templateImg.Empty())
            {
                throw new ArgumentException("源图像或模板图像为空");
            }

            // 验证模板尺寸是否合理
            if (templateImg.Width > src.Width || templateImg.Height > src.Height)
            {
                throw new ArgumentException("模板图像尺寸大于源图像");
            }

            // 验证尺寸比例是否合理（避免一边大一边小的情况）
            if ((templateImg.Width < src.Width && templateImg.Height > src.Height) ||
                (templateImg.Width > src.Width && templateImg.Height < src.Height))
            {
                throw new ArgumentException("模板图像与源图像尺寸比例不合理");
            }

            // 验证面积比例
            if (templateImg.Width * templateImg.Height > src.Width * src.Height)
            {
                throw new ArgumentException("模板图像面积大于源图像");
            }

            // 建立源图像的图像金字塔
            VectorOfMat srcPyramid = new VectorOfMat();
            
            int topLayer = TemplateData.VecPyramid.Count - 1; // 不包括原始图像层
            double D2R = Math.PI / 180.0;
            double R2D = 180.0 / Math.PI;

            // 如果需要反转源图像，则先反转源图像
            if (reverse)
            {
                Mat srcReverse = new Mat();
                Cv2.BitwiseNot(src, srcReverse); // 反转源图像
                Cv2.BuildPyramid(srcReverse, srcPyramid, topLayer);
                if (debug)
                {
                    Cv2.ImShow("Reversed Source Image", srcReverse);
                    Cv2.WaitKey(0);
                    Cv2.DestroyAllWindows();
                }
            }
            else
            {
                Cv2.BuildPyramid(src, srcPyramid, topLayer); // 不包括原始图像层
            }
            // 将VectorOfMat转换为List<Mat>
            List<Mat> srcPyramidList = srcPyramid.ToArray().ToList();

            if (debug)
            {
                for (int i = 0; i < srcPyramidList.Count; i++)
                {
                    // 转换为彩色以便添加注释
                    Mat displayImg = new Mat();
                    Cv2.CvtColor(srcPyramidList[i], displayImg, ColorConversionCodes.GRAY2BGR);

                    // 添加缩放比例标注
                    string scaleText = $"Scale: 1/{1 << i}";
                    Cv2.PutText(displayImg, scaleText, new Point(0, 10), HersheyFonts.HersheySimplex,
                        0.3, new Scalar(0, 255, 0), 1);
                    Cv2.ImShow($"Pyramid layer {i} ({srcPyramidList[i].Width}x{srcPyramidList[i].Height})",
                        displayImg);
                    Cv2.WaitKey(0);
                }
                Cv2.DestroyAllWindows();
            }

            Mat topTemplLayer = TemplateData.VecPyramid[topLayer];
            Mat topSrcLayer = srcPyramidList[topLayer];

            // 第一阶段，以最顶层找出大致角度和ROI
            if (autoAngleStep)
            {
                // 第一阶段，以最顶层找出大致角度和ROI
                // 计算角度步进值
                // 使用 arctan(2/max(width,height)) 来确定合适的角度步进
                angleStep = Math.Atan(2.0 / Math.Max(topTemplLayer.Cols, topTemplLayer.Rows)) * R2D;

                if (debug)
                {
                    Console.WriteLine($"Top layer size: {topTemplLayer.Width}x{topTemplLayer.Height}");
                    Console.WriteLine($"Angle step: {angleStep:F2}°");
                }
            }

            List<double> angles = new List<double>();
            if (angleRange > 0)
            {
                // 计算角度范围内的角度
                for (double angle = startAngle; angle < startAngle + angleRange + angleStep; angle += angleStep)
                {
                    angles.Add(angle);
                }
            }
            else
            {
                // 如果角度范围为0，则只使用起始角度
                // if (startAngle == 0)
                // {
                //     angles.Add(startAngle);
                // }
                // else
                // {
                //     for (int i = -1; i <= 1; i++)
                //     {
                //         angles.Add(startAngle + angleStep * i);
                //     }
                // }
                angles.Add(startAngle);
            }

            if (debug)
            {
                Console.WriteLine($"Total angles to check: {angles.Count}");
            }

            int topSrcWidth = srcPyramidList[srcPyramidList.Count - 1].Width;
            int topSrcHeight = srcPyramidList[srcPyramidList.Count - 1].Height;
            Point2f topSrcCenter = new Point2f((float)((topSrcWidth - 1) / 2.0f), (float)((topSrcHeight - 1) / 2.0f));
            List<MatchParameter> vecMatchParameters = new List<MatchParameter>();

            // 为每一层设置评分权重
            List<double> layerScores = new List<double>();
            double baseScore = scoreThreshold;
            for (int i = 0; i <= topLayer; i++)
            {
                layerScores.Add(baseScore);
                baseScore *= 0.9; // 每层降低10%

            }

            if (debug)
            {
                Console.WriteLine("Layer weights:");
                for (int i = 0; i < layerScores.Count; i++)
                {
                    Console.WriteLine($"Layer {i}: {layerScores[i]:F3}");
                }
            }

            bool useBlockCalculation = (topSrcLayer.Width * topSrcLayer.Height) /
                (topTemplLayer.Width * topTemplLayer.Height) > 500 && maxMatchCount > 10;

            if (debug && useBlockCalculation)
            {
                Console.WriteLine("Using block-based calculation due to large image size ratio");
                Console.WriteLine($"Source size: {topSrcLayer.Width}x{topSrcLayer.Height}");
                Console.WriteLine($"Template size: {topTemplLayer.Width}x{topTemplLayer.Height}");
                Console.WriteLine(
                    $"Area ratio: {(topSrcLayer.Width * topSrcLayer.Height) / (topTemplLayer.Width * topTemplLayer.Height):F2}");
            }

            // 匹配每个角度和每层金字塔图像
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < angles.Count; i++)
            {
                Mat matR = Cv2.GetRotationMatrix2D(topSrcCenter, angles[i], 1.0);
                Mat matResult = new Mat();
                Point ptMaxLoc = new Point();
                double dValue = 0.0;
                double dMaxVal = 0.0;
                Size sizeBest = GetBestRotationSize(srcPyramidList[topLayer].Size(),
                    TemplateData.VecPyramid[topLayer].Size(), angles[i]);

                float fTranslationX = (sizeBest.Width - 1) / 2.0f - topSrcCenter.X;
                float fTranslationY = (sizeBest.Height - 1) / 2.0f - topSrcCenter.Y;
                matR.At<double>(0, 2) += fTranslationX; // 添加平移到旋转矩阵
                matR.At<double>(1, 2) += fTranslationY; // 添加平移到旋转矩阵
                if (debug)
                {
                    // 打印 matR 矩阵内容
                    Console.WriteLine("matR:");
                    for (int row = 0; row < matR.Rows; row++)
                    {
                        for (int col = 0; col < matR.Cols; col++)
                        {
                            Console.Write($"{matR.At<double>(row, col):F6}\t");
                        }
                        Console.WriteLine();
                    }
                }
                Mat matRotatedSrc = new Mat();
                Cv2.WarpAffine(srcPyramidList[topLayer], matRotatedSrc, matR, sizeBest, InterpolationFlags.Linear,
                    BorderTypes.Constant, new Scalar(TemplateData.BorderColor));

                if (debug)
                {
                    Cv2.ImShow("src", srcPyramidList[topLayer]);
                    Cv2.ImShow("rotatedSrc", matRotatedSrc);
                    Cv2.WaitKey(0);
                    Cv2.DestroyAllWindows();
                }

                MatchTemplate(matRotatedSrc, TemplateData, ref matResult, topLayer, false);

                if (useBlockCalculation)
                {
                    BlockMax blockMax = new BlockMax(matResult, TemplateData.VecPyramid[topLayer].Size());
                    blockMax.GetMaxValueLoc(out dMaxVal, out ptMaxLoc);
                    if (dMaxVal < layerScores[topLayer])
                    {
                        continue;
                    }

                    vecMatchParameters.Add(new MatchParameter(
                        new Point2d(ptMaxLoc.X - fTranslationX, ptMaxLoc.Y - fTranslationY), dMaxVal,
                        angles[i]));
                    for (int j = 0; j < maxMatchCount + MatchCandidateNum - 1; j++)
                    {
                        ptMaxLoc = GetNextMaxLoc(ref matResult, ptMaxLoc, topTemplLayer.Size(), ref dValue, maxOverlap,
                            ref blockMax);
                        if (dValue < layerScores[topLayer])
                        {
                            break;
                        }

                        vecMatchParameters.Add(new MatchParameter(
                            new Point2d(ptMaxLoc.X - fTranslationX, ptMaxLoc.Y - fTranslationY), dValue,
                            angles[i]));
                    }
                }
                else
                {
                    Cv2.MinMaxLoc(matResult, out _, out dMaxVal, out _, out ptMaxLoc);
                    if (dMaxVal < layerScores[topLayer])
                    {
                        continue;
                    }
                    vecMatchParameters.Add(new MatchParameter(
                        new Point2d(ptMaxLoc.X - fTranslationX, ptMaxLoc.Y - fTranslationY), dMaxVal,
                        angles[i]));
                    for (int j = 0; j < maxMatchCount + MatchCandidateNum - 1; j++)
                    {
                        ptMaxLoc = GetNextMaxLoc(ref matResult, ptMaxLoc, topTemplLayer.Size(), ref dValue, maxOverlap);
                        if (dValue < layerScores[topLayer])
                        {
                            break;
                        }
                        vecMatchParameters.Add(new MatchParameter(
                            new Point2d(ptMaxLoc.X - fTranslationX, ptMaxLoc.Y - fTranslationY), dValue,
                            angles[i]));
                    }
                }
            }

            // 匹配结束计时
            stopwatch.Stop();
            if (debug)
            {
                // 匹配时间消耗
                Console.WriteLine($"第一阶段匹配耗时: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            }
            // 对vecMatchParameters排序
            vecMatchParameters.Sort(CompareScoreBig2Small);

            int iMatchSize = vecMatchParameters.Count;
            int iDstWidth = topTemplLayer.Width;
            int iDstHeight = topTemplLayer.Height;

            // 可视化第一阶段结果
            if (debug)
            {
                int iDebugScale = 2;

                Mat matShow = new Mat();
                Mat matResize = new Mat();

                Cv2.Resize(topSrcLayer, matResize, new Size(topSrcWidth * iDebugScale, topSrcHeight * iDebugScale));
                Cv2.CvtColor(matResize, matShow, ColorConversionCodes.GRAY2BGR);
                string str = $"Toplayer: {iMatchSize}";
                List<Point2f> vec = new List<Point2f>();
                for (int i = 0; i < iMatchSize; i++)
                {
                    double dAngle = -vecMatchParameters[i].DMatchAngle * Math.PI / 180;

                    Point2f ptLT = ptRotatedPt2f(
                        new Point2f((float)vecMatchParameters[i].Pt.X, (float)vecMatchParameters[i].Pt.Y),
                        topSrcCenter, dAngle);

                    Point2f ptRT = new Point2f(
                        ptLT.X + iDstWidth * (float)Math.Cos(dAngle),
                        ptLT.Y - iDstWidth * (float)Math.Sin(dAngle));

                    Point2f ptLB = new Point2f(
                        ptLT.X + iDstHeight * (float)Math.Sin(dAngle),
                        ptLT.Y + iDstHeight * (float)Math.Cos(dAngle));

                    Point2f ptRB = new Point2f(
                        ptRT.X + iDstHeight * (float)Math.Sin(dAngle),
                        ptRT.Y + iDstHeight * (float)Math.Cos(dAngle));

                    // 绘制矩形
                    Cv2.Line(matShow, new Point((ptLT * iDebugScale).X, (ptLT * iDebugScale).Y),
                        new Point((ptLB * iDebugScale).X, (ptLB * iDebugScale).Y), new Scalar(0, 255, 0));
                    Cv2.Line(matShow, new Point((ptLB * iDebugScale).X, (ptLB * iDebugScale).Y),
                        new Point((ptRB * iDebugScale).X, (ptRB * iDebugScale).Y), new Scalar(0, 255, 0));
                    Cv2.Line(matShow, new Point((ptRB * iDebugScale).X, (ptRB * iDebugScale).Y),
                        new Point((ptRT * iDebugScale).X, (ptRT * iDebugScale).Y), new Scalar(0, 255, 0));
                    Cv2.Line(matShow, new Point((ptRT * iDebugScale).X, (ptRT * iDebugScale).Y),
                        new Point((ptLT * iDebugScale).X, (ptLT * iDebugScale).Y), new Scalar(0, 255, 0));

                    // 绘制左上角圆点
                    Cv2.Circle(matShow, new Point((ptLT * iDebugScale).X, (ptLT * iDebugScale).Y), 1,
                        new Scalar(0, 0, 255));

                    // 存储角点
                    vec.Add(ptLT * iDebugScale);
                    vec.Add(ptRT * iDebugScale);
                    vec.Add(ptLB * iDebugScale);
                    vec.Add(ptRB * iDebugScale);

                    // 绘制索引文本
                    string strText = $"{i}";
                    Cv2.PutText(matShow, strText, new Point((ptLT * iDebugScale).X, (ptLT * iDebugScale).Y),
                        HersheyFonts.HersheyPlain, 1, new Scalar(0, 255, 0));
                }

                Cv2.ImShow(str, matShow);
                Cv2.WaitKey(0);
                Cv2.DestroyAllWindows();
            }

            // 第二阶段，基于第一阶段的结果进行精确匹配
            int iStopLayer = fastMode ? 1: 0; //设置为1时：粗匹配，牺牲精度提升速度。
            List<MatchParameter> vecAllResult = new List<MatchParameter>();
            for (int i = 0; i < vecMatchParameters.Count; i++)
            {
                double dAngle = -vecMatchParameters[i].DMatchAngle * D2R;
                Point2f ptLT = ptRotatedPt2f(
                    new Point2f((float)vecMatchParameters[i].Pt.X, (float)vecMatchParameters[i].Pt.Y),
                    topSrcCenter, dAngle);

                if (autoAngleStep)
                {
                    // 计算角度步进值
                    // 使用 arctan(2/max(width,height)) 来确定合适的角度步进
                    angleStep = Math.Atan(2.0 / Math.Max(topTemplLayer.Cols, topTemplLayer.Rows)) * R2D;

                    if (debug)
                    {
                        Console.WriteLine($"Top layer size: {topTemplLayer.Width}x{topTemplLayer.Height}");
                        Console.WriteLine($"Angle step: {angleStep:F2}°");
                    }
                }

                vecMatchParameters[i].DAngleStart = vecMatchParameters[i].DMatchAngle - angleStep;
                vecMatchParameters[i].DAngleEnd = vecMatchParameters[i].DMatchAngle + angleStep;

                if (topLayer <= iStopLayer)
                {
                    vecMatchParameters[i].Pt = new Point2d(ptLT.X * ((iStopLayer == 0) ? 1 : 2),
                        ptLT.Y * ((iStopLayer == 0) ? 1 : 2));
                    vecAllResult.Add(vecMatchParameters[i]);
                }
                else
                {
                    for (int iLayer = topLayer - 1; iLayer >= iStopLayer; iLayer--)
                    {
                        if (autoAngleStep)
                        {
                            // 计算角度步进值
                            // 使用 arctan(2/max(width,height)) 来确定合适的角度步进
                            angleStep = Math.Atan(2.0 / Math.Max(TemplateData.VecPyramid[iLayer].Cols,
                                TemplateData.VecPyramid[iLayer].Rows)) * R2D;

                            if (debug)
                            {
                                Console.WriteLine($"Top layer size: {TemplateData.VecPyramid[iLayer].Width}x{TemplateData.VecPyramid[iLayer].Height}");
                                Console.WriteLine($"Angle step: {angleStep:F2}°");
                            }
                        }

                        // 计算旋转角度
                        List<double> vecAngles = new List<double>();
                        double dMatchedAngle = vecMatchParameters[i].DMatchAngle;
                        if (angleRange > 0)
                        {
                            for (int j = -1; j <= 1; j++)
                            {
                                vecAngles.Add(dMatchedAngle + angleStep * j);
                            }
                        }
                        else
                        {
                            // if (startAngle == 0)
                            // {
                            //     vecAngles.Add(startAngle);
                            // }
                            // else
                            // {
                            //     for (int j = -1; j <= 1; j++)
                            //     {
                            //         vecAngles.Add(dMatchedAngle + angleStep * j);
                            //     }
                            // }
                            vecAngles.Add(dMatchedAngle);
                        }

                        if (debug)
                        {
                            Console.WriteLine($"Total angles to check: {vecAngles.Count}");
                            foreach (var angle in vecAngles)
                            {
                                Console.WriteLine($"Angle: {angle:F2}°");
                            }
                        }

                        Point2f ptSrcCenter = new Point2f((float)((srcPyramidList[iLayer].Cols - 1) / 2.0),
                            (float)((srcPyramidList[iLayer].Rows - 1) / 2.0));
                        List<MatchParameter> vecNewMatchParameters = new List<MatchParameter>();
                        int iMaxScoreIndex = 0;
                        double dBigValue = -1;
                        for (int j = 0; j < vecAngles.Count; j++)
                        {
                            Mat matRotatedSrc = new Mat();
                            Mat matResult = new Mat();
                            double dMaxValue = 0.0;
                            Point ptMaxLoc = new Point();
                            GetRotatedRoi(srcPyramidList[iLayer], TemplateData.VecPyramid[iLayer].Size(), ptLT * 2,
                                vecAngles[j], ref matRotatedSrc);

                            MatchTemplate(matRotatedSrc, TemplateData, ref matResult, iLayer, true);
                            Cv2.MinMaxLoc(matResult, out _, out dMaxValue, out _, out ptMaxLoc);
                            vecNewMatchParameters.Add( new MatchParameter(ptMaxLoc, dMaxValue,
                                vecAngles[j]));

                            if (vecNewMatchParameters[j].DMatchScore > dBigValue)
                            {
                                dBigValue = vecNewMatchParameters[j].DMatchScore;
                                iMaxScoreIndex = j;
                            }
                            // 次像素估计
                            if (ptMaxLoc.X == 0 ||
                                ptMaxLoc.Y == 0 ||
                                ptMaxLoc.X == matResult.Cols - 1 ||
                                ptMaxLoc.Y == matResult.Rows - 1)
                            {
                                vecNewMatchParameters[j].BPosOnBorder = true;
                            }

                            if (!vecNewMatchParameters[j].BPosOnBorder)
                            {
                                for (int y = -1; y <= 1; y++)
                                {
                                    for (int x = -1; x <= 1; x++)
                                    {
                                        vecNewMatchParameters[j].VecResult[x + 1, y + 1] =
                                            matResult.At<float>(ptMaxLoc.Y + y, ptMaxLoc.X + x);
                                    }
                                }
                            }
                        }

                        if (vecNewMatchParameters[iMaxScoreIndex].DMatchScore < layerScores[iLayer])
                        {
                            break;
                        }

                        // 次像素估计
                        if (subPixelEstimation &&
                            iLayer == 0 &&
                            (!vecNewMatchParameters[iMaxScoreIndex].BPosOnBorder) &&
                            iMaxScoreIndex != 0 && 
                            iMaxScoreIndex != 2)
                        {
                            double dNewX = 0;
                            double dNewY = 0;
                            double dNewAngle = 0;
                            SubPixelEstimation(ref vecNewMatchParameters, ref dNewX, ref dNewY,
                                ref dNewAngle, angleStep, iMaxScoreIndex);
                            vecNewMatchParameters[iMaxScoreIndex].Pt = new Point2d(dNewX, dNewY);
                            vecNewMatchParameters[iMaxScoreIndex].DMatchAngle = dNewAngle;
                        }

                        double dNewMatchAngle = vecNewMatchParameters[iMaxScoreIndex].DMatchAngle;
                        Point2f ptPaddingLT = ptRotatedPt2f(ptLT * 2, ptSrcCenter, dNewMatchAngle * D2R) -
                                              new Point2f(3, 3);
                        Point2f pt = new Point2f((float)(vecNewMatchParameters[iMaxScoreIndex].Pt.X + ptPaddingLT.X),
                            (float)(vecNewMatchParameters[iMaxScoreIndex].Pt.Y + ptPaddingLT.Y));
                        pt = ptRotatedPt2f(pt, ptSrcCenter, -dNewMatchAngle * D2R);
                        if (iLayer == iStopLayer)
                        {
                            vecNewMatchParameters[iMaxScoreIndex].Pt = new Point2d(pt.X * (iStopLayer == 0 ? 1 : 2),
                                pt.Y * (iStopLayer == 0 ? 1 : 2));
                            vecAllResult.Add(vecNewMatchParameters[iMaxScoreIndex]);
                        }
                        else
                        {
                            vecMatchParameters[i].DMatchAngle = dNewMatchAngle;
                            vecMatchParameters[i].DAngleStart = vecMatchParameters[i].DMatchAngle - angleStep / 2;
                            vecMatchParameters[i].DAngleEnd = vecMatchParameters[i].DMatchAngle + angleStep / 2;
                            ptLT = pt;
                        }
                    }
                }
            }

            // 过滤
            FilterWithScore(ref vecAllResult, scoreThreshold);
            // 去重
            iDstWidth = TemplateData.VecPyramid[iStopLayer].Cols * (iStopLayer == 0 ? 1 : 2);
            iDstHeight = TemplateData.VecPyramid[iStopLayer].Rows * (iStopLayer == 0 ? 1 : 2);

            for (int i = 0; i < vecAllResult.Count; i++)
            {
                Point2f ptLT = new Point2f();
                Point2f ptRT = new Point2f();
                Point2f ptLB = new Point2f();
                Point2f ptRB = new Point2f();
                double dAngle = -vecAllResult[i].DMatchAngle * D2R;
                ptLT = new Point2f((float)vecAllResult[i].Pt.X, (float)vecAllResult[i].Pt.Y);
                ptRT = new Point2f(
                    ptLT.X + iDstWidth * (float)Math.Cos(dAngle),
                    ptLT.Y - iDstWidth * (float)Math.Sin(dAngle));
                ptLB = new Point2f(ptLT.X +iDstHeight * (float)Math.Sin(dAngle),
                    ptLT.Y + iDstHeight * (float)Math.Cos(dAngle));
                ptRB = new Point2f(ptRT.X + iDstHeight * (float)Math.Sin(dAngle),
                    ptRT.Y + iDstHeight * (float)Math.Cos(dAngle));
                // 记录旋转矩形
                vecAllResult[i].RectR = new RotatedRect(ptLT, ptRT, ptRB);
            }

            FilterWithRotateRect(ref vecAllResult, TemplateMatchModes.CCoeffNormed, maxOverlap);
            
            // 根据分数排序
            vecAllResult.Sort(CompareScoreBig2Small);

            iDstWidth = TemplateData.VecPyramid[0].Cols;
            iDstHeight = TemplateData.VecPyramid[0].Rows;
            Matches = new List<SingleMatchedTarget>();
            for (int i = 0; i < vecAllResult.Count; i++)
            {
                SingleMatchedTarget sstm = new SingleMatchedTarget();
                double dAngle = -vecAllResult[i].DMatchAngle * D2R;

                sstm.LeftTop = vecAllResult[i].Pt;
                sstm.RightTop = new Point2d(
                    sstm.LeftTop.X + iDstWidth * Math.Cos(dAngle),
                    sstm.LeftTop.Y - iDstWidth * Math.Sin(dAngle));
                sstm.LeftBottom = new Point2d(
                    sstm.LeftTop.X + iDstHeight * Math.Sin(dAngle),
                    sstm.LeftTop.Y + iDstHeight * Math.Cos(dAngle));
                sstm.RightBottom = new Point2d(
                    sstm.RightTop.X + iDstHeight * Math.Sin(dAngle),
                    sstm.RightTop.Y + iDstHeight * Math.Cos(dAngle));
                sstm.Center =
                    new Point2d((sstm.LeftTop.X + sstm.RightTop.X + sstm.LeftBottom.X + sstm.RightBottom.X) / 4.0,
                        (sstm.LeftTop.Y + sstm.RightTop.Y + sstm.LeftBottom.Y + sstm.RightBottom.Y) / 4.0);
                sstm.Angle = -vecAllResult[i].DMatchAngle;
                sstm.Score = vecAllResult[i].DMatchScore;
                if (sstm.Angle < -180)
                {
                    sstm.Angle += 360;
                }

                if (sstm.Angle > 180)
                {
                    sstm.Angle -= 360;
                }

                sstm.Index = i;
                Matches.Add(sstm);

                // 如果已经达到最大匹配数量，则退出
                if (i + 1 == maxMatchCount)
                {
                    break;
                }
            }

            MatchedTargetNum = Matches.Count;
            return MatchedTargetNum;
            //throw new NotImplementedException();
        }

        private void FilterWithRotateRect(ref List<MatchParameter> vec, TemplateMatchModes iMethod, double maxOverlap)
        {
            RotatedRect rect1 = new RotatedRect();
            RotatedRect rect2 = new RotatedRect();

            for (int i = 0; i < vec.Count; i++)
            {
                if (vec[i].BDelete)
                {
                    continue;
                }

                for (int j = i + 1; j < vec.Count; j++)
                {
                    if (vec[j].BDelete)
                    {
                        continue;
                    }

                    rect1 = vec[i].RectR;
                    rect2 = vec[j].RectR;
                    Point2f[] vecInterSec;
                    RectanglesIntersectTypes iInterSecType =
                        Cv2.RotatedRectangleIntersection(rect1, rect2, out vecInterSec);
                    if (iInterSecType == RectanglesIntersectTypes.None)
                    {
                        // 无交集
                        continue;
                    }
                    else if (iInterSecType == RectanglesIntersectTypes.Full)
                    {
                        // 包含
                        int iDeleteIndex = 0;
                        if (iMethod == TemplateMatchModes.SqDiff)
                        {
                            iDeleteIndex = vec[i].DMatchScore <= vec[j].DMatchScore ? j : i;
                        }
                        else
                        {
                            iDeleteIndex = vec[i].DMatchScore >= vec[j].DMatchScore ? j : i;
                        }

                        vec[iDeleteIndex].BDelete = true;
                    }
                    else
                    {
                        // 部分重叠
                        if (vecInterSec.Length < 3)
                        {
                            continue;
                        }
                        else
                        {
                            int iDeleteIndex = 0;
                            // 计算重叠面积
                            SortPtWithCenter(ref vecInterSec);
                            double dArea = Cv2.ContourArea(vecInterSec);
                            double dRatio = dArea / (rect1.Size.Width * rect1.Size.Height);
                            // 若大于最大交叠比例，选分数高的
                            if (dRatio > maxOverlap)
                            {
                                if (iMethod == TemplateMatchModes.SqDiff)
                                {
                                    iDeleteIndex = vec[i].DMatchScore <= vec[j].DMatchScore ? j : i;
                                }
                                else
                                {
                                    iDeleteIndex = vec[i].DMatchScore >= vec[j].DMatchScore ? j : i;
                                }
                                vec[iDeleteIndex].BDelete = true;
                            }
                        }
                    }
                }
            }

            // 删除标记为删除的元素
            vec.RemoveAll(x => x.BDelete);
        }

        /// <summary>
        /// 按照与中心点的距离对交点进行排序
        /// </summary>
        /// <param name="vecInterSec">点集</param>
        private void SortPtWithCenter(ref Point2f[] vecInterSec)
        {
            // 计算中心点
            Point2f ptCenter = new Point2f();
            for (int i = 0; i < vecInterSec.Length; i++)
            {
                ptCenter += vecInterSec[i];
            }
            ptCenter.X /= vecInterSec.Length;
            ptCenter.Y /= vecInterSec.Length;

            Point2f vecX = new Point2f(1, 0);
            List<KeyValuePair<Point2f, double>> vecPtAngle = new List<KeyValuePair<Point2f, double>>();
            for (int i = 0; i < vecInterSec.Length; i++)
            {
                Point2f vec1 = new Point2f(vecInterSec[i].X - ptCenter.X, vecInterSec[i].Y - ptCenter.Y);
                double fNormaVec1 = vec1.X * vec1.X + vec1.Y * vec1.Y;
                double fDot = vec1.X;

                if (vec1.Y < 0)
                {
                    // 若点在中心的上方
                    vecPtAngle.Add(new KeyValuePair<Point2f, double>(vecInterSec[i],
                        Math.Acos(fDot / fNormaVec1) * 180.0 / Math.PI));
                }
                else if (vec1.Y > 0)
                {
                    // 若点在中心的下方
                    vecPtAngle.Add(new KeyValuePair<Point2f, double>(vecInterSec[i],
                        360.0 - Math.Acos(fDot / fNormaVec1) * 180.0 / Math.PI));
                }
                else
                {
                    // 若点在中心的水平线上
                    if (vec1.X - ptCenter.X > 0)
                    {
                        vecPtAngle.Add(new KeyValuePair<Point2f, double>(vecInterSec[i], 0));
                    }
                    else
                    {
                        vecPtAngle.Add(new KeyValuePair<Point2f, double>(vecInterSec[i], 180));
                    }
                }

            }

            vecPtAngle.Sort(ComparePtWithAngle);
            for (int i = 0; i < vecInterSec.Length; i++)
            {
                vecInterSec[i] = vecPtAngle[i].Key;
            }
        }

        /// <summary>
        /// 比较点的角度，从小到大
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private int ComparePtWithAngle(KeyValuePair<Point2f, double> x, KeyValuePair<Point2f, double> y)
        {
            return y.Value.CompareTo(x.Value);
        }

        /// <summary>
        /// 根据得分过滤匹配结果
        /// </summary>
        /// <param name="vec">匹配结果</param>
        /// <param name="dScore">得分阈值</param>
        private void FilterWithScore(ref List<MatchParameter> vec, double dScore)
        {
            vec.Sort(CompareScoreBig2Small);
            int iIndexDelete = vec.Count + 1;
            for (int i = 0; i < vec.Count; i++)
            {
                if (vec[i].DMatchScore < dScore)
                {
                    iIndexDelete = i;
                    break;
                }
            }
            if (iIndexDelete == vec.Count + 1)
            {
                return;
            }

            vec.RemoveRange(iIndexDelete, vec.Count - iIndexDelete);
        }

        private void SubPixelEstimation(ref List<MatchParameter> vec, ref double dNewX, ref double dNewY,
            ref double dNewAngle, double angleStep, int iMaxScoreIndex)
        {
            // 构建最小二乘法方程
            Mat matA = new Mat(27, 10, MatType.CV_64F); // 系数矩阵A
            Mat matZ = new Mat(10, 1, MatType.CV_64F); // 待求解的向量Z
            Mat matS = new Mat(27, 1, MatType.CV_64F); // 常数项S

            // 获取最高分数点的坐标和角度
            double dXMaxScore = vec[iMaxScoreIndex].Pt.X;
            double dYMaxScore = vec[iMaxScoreIndex].Pt.Y;
            double dTheataMaxScore = vec[iMaxScoreIndex].DMatchAngle;

            // 构建方程组
            int iRow = 0;
            for (int theta = 0; theta <= 2; theta++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        // 矩阵A的各项系数:xx yy tt xy xt yt x y t 1
                        //xx yy tt xy xt yt x y t 1
                        //0  1  2  3  4  5  6 7 8 9
                        double dX = dXMaxScore + x;
                        double dY = dYMaxScore + y;
                        double dT = (dTheataMaxScore + (theta - 1) * angleStep) * Math.PI / 180.0;
                        // 填充矩阵A的第一行
                        matA.At<double>(iRow, 0) = dX * dX; // xx
                        matA.At<double>(iRow, 1) = dY * dY; // yy
                        matA.At<double>(iRow, 2) = dT * dT; // tt
                        matA.At<double>(iRow, 3) = dX * dY; // xy
                        matA.At<double>(iRow, 4) = dX * dT; // xt
                        matA.At<double>(iRow, 5) = dY * dT; // yt
                        matA.At<double>(iRow, 6) = dX; // x
                        matA.At<double>(iRow, 7) = dY; // y
                        matA.At<double>(iRow, 8) = dT; // t
                        matA.At<double>(iRow, 9) = 1.0; // 1
                        // 填充矩阵S的第一行
                        matS.At<double>(iRow, 0) = vec[iMaxScoreIndex + (theta - 1)].VecResult[x + 1, y + 1]; // S
                        iRow++;
                    }
                }
            }

            // 求解方程组获得系数矩阵Z
            // 求解Z矩陣，得到k0~k9
            // [ x* ] = [ 2k0 k3 k4 ]-1 [ -k6 ]
            // | y* | = | k3 2k1 k5 |   | -k7 |
            // [ t* ] = [ k4 k5 2k2 ]   [ -k8 ]

            matZ = (matA.T() * matA).Inv() * matA.T() * matS;
            Mat matZT = new Mat();
            Cv2.Transpose(matZ, matZT);
            double[] dZ = new double[matZT.Cols];
            matZT.GetArray<double>(out dZ);
            Mat matK1 = new Mat(3, 3, MatType.CV_64F);
            matK1.SetArray<double>(new double[] { 
                (2 * dZ[0]), dZ[3], dZ[4] ,
                dZ[3], (2 * dZ[1]), dZ[5],
                dZ[4], dZ[5], (2 * dZ[2])});
            Mat matK2 = new Mat(3, 1, MatType.CV_64F);
            matK2.SetArray<double>(new double[] { -dZ[6], -dZ[7], -dZ[8] });
            Mat matDelta = matK1.Inv() * matK2;

            // 输出结果
            dNewX = matDelta.At<double>(0, 0);
            dNewY = matDelta.At<double>(1, 0);
            dNewAngle = matDelta.At<double>(2, 0) * 180.0 / Math.PI; // 转换为角度
        }

        private void GetRotatedRoi(Mat matSrc, Size size, Point2f ptLT, double dAngle, ref Mat matRoi)
        {
            double dAngleRad = dAngle * Math.PI / 180.0;
            Point2f ptCenter = new Point2f((float)(matSrc.Cols - 1) / 2.0f, (float)(matSrc.Rows - 1) / 2.0f);
            Point2f ptRotatedLT = ptRotatedPt2f(ptLT, ptCenter, dAngleRad);
            Size sizePadding = new Size(size.Width + 6, size.Height + 6);

            Mat matRotated = Cv2.GetRotationMatrix2D(ptCenter, dAngle, 1);
            matRotated.At<double>(0, 2) -= ptRotatedLT.X - 3;
            matRotated.At<double>(1, 2) -= ptRotatedLT.Y - 3;
            Cv2.WarpAffine(matSrc, matRoi, matRotated, sizePadding);
        }

        private int CompareScoreBig2Small(MatchParameter x, MatchParameter y)
        {
            return y.DMatchScore.CompareTo(x.DMatchScore);
        }

        private Point GetNextMaxLoc(ref Mat matResult, Point ptMaxLoc, Size sizeTemplate, ref double dMaxValue, double maxOverlap)
        {
            int iStartX = (int)(ptMaxLoc.X - sizeTemplate.Width * (1 - maxOverlap));
            int iStartY = (int)(ptMaxLoc.Y - sizeTemplate.Height * (1 - maxOverlap));
            Rect rectIgnore = new Rect(
                iStartX, 
                iStartY, 
                (int)(2 * sizeTemplate.Width * (1 - maxOverlap)), 
                (int)(2 * sizeTemplate.Height * (1 - maxOverlap)));
            // 在结果矩阵中填充忽略区域
            Cv2.Rectangle(matResult, rectIgnore, new Scalar(-1), -1);
            // 得到下一个最大值
            Point ptReturn = new Point();
            Cv2.MinMaxLoc(matResult, out _, out dMaxValue, out _, out ptReturn);
            return ptReturn;
        }

        /// <summary>
        /// 获取下一个最大匹配位置（基于块匹配的方式）
        /// </summary>
        /// <param name="matResult">匹配结果矩阵</param>
        /// <param name="ptMaxLoc">当前最大位置</param>
        /// <param name="sizeTemplate">模板尺寸</param>
        /// <param name="dMaxValue">返回的最大值</param>
        /// <param name="maxOverlap">最大重叠率</param>
        /// <param name="blockMax">块匹配对象</param>
        /// <returns></returns>
        private Point GetNextMaxLoc(ref Mat matResult, Point ptMaxLoc, Size sizeTemplate, ref double dMaxValue, double maxOverlap, ref BlockMax blockMax)
        {
            int iStartX = (int)(ptMaxLoc.X - sizeTemplate.Width * (1 - maxOverlap));
            int iStartY = (int)(ptMaxLoc.Y - sizeTemplate.Height * (1 - maxOverlap));
            Rect rectIgnore = new Rect(iStartX, iStartY, (int)(2 * sizeTemplate.Width * (1 - maxOverlap)),
                (int)(2 * sizeTemplate.Height * (1 - maxOverlap)));
            // 在结果矩阵中填充忽略区域
            Cv2.Rectangle(matResult, rectIgnore, new Scalar(1), -1);
            blockMax.UpdateMax(rectIgnore);
            // 获取下一个最大值位置
            Point ptReturn = new Point();
            blockMax.GetMaxValueLoc(out dMaxValue, out ptReturn);
            return ptReturn;
        }

        /// <summary>
        /// 模板匹配函数，使用归一化互相关(NCC)进行匹配
        /// </summary>
        /// <param name="matSrc">源图像</param>
        /// <param name="templData">模板数据</param>
        /// <param name="matResult">匹配结果</param>
        /// <param name="iLayer">金字塔层级</param>
        /// <param name="useSIMD">是否使用SIMD加速</param>
        private void MatchTemplate(Mat matSrc, TemplData templData, ref Mat matResult, int iLayer, bool useSIMD)
        {
            if (useSIMD)
            {
                unsafe
                {
                    // 创建结果矩阵
                    Mat matTemplate = templData.VecPyramid[iLayer].Clone();

                    int resultRows = matSrc.Rows - matTemplate.Rows + 1;
                    int resultCols = matSrc.Cols - matTemplate.Cols + 1;

                    matResult.Create(resultRows, resultCols, MatType.CV_32FC1);
                    matResult.SetTo(0);

                    byte* srcPtr = matSrc.DataPointer;
                    byte* templPtr = matTemplate.DataPointer;
                    byte* resultPtr = matResult.DataPointer;

                    // 使用 SIMD 进行卷积
                    SIMDWrapper.MatchTemplate_SIMD(srcPtr, matSrc.Width, matSrc.Height, (int)matSrc.Step(), templPtr,
                        matTemplate.Width, matTemplate.Height, (int)matTemplate.Step(), resultPtr, matResult.Width,
                        matResult.Height, (int)matResult.Step() / sizeof(float));
                }
            }
            else
            {
                Cv2.MatchTemplate(matSrc, templData.VecPyramid[iLayer], matResult, TemplateMatchModes.CCorr);
            }

            // 计算归一化系数
            CCOEFFDenominator(matSrc, templData, ref matResult, iLayer);
        }

        /// <summary>
        /// 计算匹配结果的归一化相关系数
        /// </summary>
        /// <param name="matSrc">源图像</param>
        /// <param name="templData">模板数据</param>
        /// <param name="matResult">匹配结果</param>
        /// <param name="iLayer">金字塔层级</param>
        private void CCOEFFDenominator(Mat matSrc, TemplData templData, ref Mat matResult, int iLayer)
        {
            unsafe
            {
                // 纯色图像特殊处理
                if (templData.VecResultEqual1[iLayer])
                {
                    matResult.SetTo(1);
                    return;
                }

                // 计算积分图
                Mat sum = new Mat();
                Mat sqSum = new Mat();
                Cv2.Integral(matSrc, sum, sqSum, MatType.CV_64F);

                int tplCols = templData.VecPyramid[iLayer].Cols;
                int tplRows = templData.VecPyramid[iLayer].Rows;

                int sumstep = (int)(sum.Step() / sizeof(double));
                int sqstep = (int)(sqSum.Step() / sizeof(double));

                double dTemplMean0 = templData.VecTemplMean[iLayer][0];
                double dTemplNorm = templData.VecTemplNorm[iLayer];
                double dInvArea = templData.VecInvArea[iLayer];

                double* q0 = (double*)sqSum.Data;
                double* q1 = q0 + tplCols;
                double* q2 = (double*)((byte*)sqSum.Data + tplRows * sqSum.Step());
                double* q3 = q2 + tplCols;

                double* p0 = (double*)sum.Data;
                double* p1 = p0 + tplCols;
                double* p2 = (double*)((byte*)sum.Data + tplRows * sum.Step());
                double* p3 = p2 + tplCols;

                for (int i = 0; i < matResult.Rows; i++)
                {
                    float* rrow = (float*)matResult.Ptr(i);
                    int idx = i * sumstep;
                    int idx2 = i * sqstep;

                    for (int j = 0; j < matResult.Cols; j++, idx++, idx2++)
                    {
                        double num = rrow[j];
                        double wndMean2 = 0, wndSum2 = 0;

                        double t = p0[idx] - p1[idx] - p2[idx] + p3[idx];
                        wndMean2 += t * t;
                        num -= t * dTemplMean0;
                        wndMean2 *= dInvArea;

                        t = q0[idx2] - q1[idx2] - q2[idx2] + q3[idx2];
                        wndSum2 += t;

                        double diff2 = Math.Max(wndSum2 - wndMean2, 0);
                        if (diff2 <= Math.Min(0.5, 10 * float.Epsilon * wndSum2))
                            t = 0;
                        else
                            t = Math.Sqrt(diff2) * dTemplNorm;

                        if (Math.Abs(num) < t)
                            num /= t;
                        else if (Math.Abs(num) < t * 1.125)
                            num = num > 0 ? 1 : -1;
                        else
                            num = 0;

                        rrow[j] = (float)num;
                    }
                }
            }
        }

        /// <summary>
        /// 获取最佳旋转图像尺寸
        /// </summary>
        /// <param name="srcSize">源图像尺寸</param>
        /// <param name="dstSize">模板图像尺寸</param>
        /// <param name="dRAngle">旋转角度</param>
        /// <returns>最佳旋转图像尺寸</returns>
        private Size GetBestRotationSize(Size srcSize, Size dstSize, double dRAngle)
        {
            const double VISION_TOLERANCE = 1e-10; // 角度容差
            const double D2R = Math.PI / 180.0;   // 角度转弧度系数

            // 将角度转换为弧度
            double dRAngleRad = dRAngle * D2R;

            // 定义源图像的四个角点
            Point ptLeftTop = new Point(0, 0);
            Point ptLeftBottom = new Point(0, srcSize.Height - 1);
            Point ptRightBottom = new Point(srcSize.Width - 1, srcSize.Height - 1);
            Point ptRightTop = new Point(srcSize.Width - 1, 0);

            // 计算旋转中心点
            Point2f ptCenter = new Point2f((srcSize.Width - 1) / 2.0f, (srcSize.Height - 1) / 2.0f);

            // 计算四个角点旋转后的位置
            Point2f ptLeftTopRotated = ptRotatedPt2f(ptLeftTop, ptCenter, dRAngleRad);
            Point2f ptLeftBottomRotated = ptRotatedPt2f(ptLeftBottom, ptCenter, dRAngleRad);
            Point2f ptRightBottomRotated = ptRotatedPt2f(ptRightBottom, ptCenter, dRAngleRad);
            Point2f ptRightTopRotated = ptRotatedPt2f(ptRightTop, ptCenter, dRAngleRad);

            // 计算旋转后图像的边界
            float fTopY = Math.Max(Math.Max(ptLeftTopRotated.Y, ptLeftBottomRotated.Y),
                Math.Max(ptRightBottomRotated.Y, ptRightTopRotated.Y));
            float fBottomY = Math.Min(Math.Min(ptLeftTopRotated.Y, ptLeftBottomRotated.Y),
                Math.Min(ptRightBottomRotated.Y, ptRightTopRotated.Y));
            float fRightX = Math.Max(Math.Max(ptLeftTopRotated.X, ptLeftBottomRotated.X),
                Math.Max(ptRightBottomRotated.X, ptRightTopRotated.X));
            float fLeftX = Math.Min(Math.Min(ptLeftTopRotated.X, ptLeftBottomRotated.X),
                Math.Min(ptRightBottomRotated.X, ptRightTopRotated.X));

            // 标准化角度到0-360度范围
            if (dRAngle > 360)
            {
                dRAngle -= 360;
            }
            else if (dRAngle < 0)
            {
                dRAngle += 360;
            }

            // 处理特殊角度情况
            if (Math.Abs(Math.Abs(dRAngle) - 90) < VISION_TOLERANCE ||
                Math.Abs(Math.Abs(dRAngle) - 270) < VISION_TOLERANCE)
            {
                return new Size(srcSize.Height, srcSize.Width);
            }
            else if (Math.Abs(dRAngle) < VISION_TOLERANCE ||
                     Math.Abs(Math.Abs(dRAngle) - 180) < VISION_TOLERANCE)
            {
                return srcSize;
            }

            // 将角度归一化到0-90度范围
            double dAngle = dRAngle;
            if (dAngle >= 0 && dAngle <= 90)
            {
                ;
            }
            else if (dAngle > 90 && dAngle <= 180)
            {
                dAngle -= 90;
            }
            else if (dAngle > 180 && dAngle <= 270)
            {
                dAngle -= 180;
            }
            else if (dAngle > 270 && dAngle <= 360)
            {
                dAngle -= 270;
            }
            else if (!(dAngle >= 0 && dAngle <= 90))
            {
                throw new ArgumentException("无效的角度值");
            }

            // 计算旋转后的高度和宽度补偿
            float fH1 = dstSize.Width * (float)(Math.Sin(dAngle * D2R) * Math.Cos(dAngle * D2R));
            float fH2 = dstSize.Height * (float)(Math.Sin(dAngle * D2R) * Math.Cos(dAngle * D2R));

            // 计算半高和半宽
            int iHalfHeight = (int)Math.Ceiling(fTopY - ptCenter.Y - fH1);
            int iHalfWidth = (int)Math.Ceiling(fRightX - ptCenter.X - fH2);

            // 计算最终尺寸
            Size sizeRet = new Size(iHalfWidth * 2, iHalfHeight * 2);

            // 检查计算结果是否合理
            bool bWrongSize = (dstSize.Width < sizeRet.Width && dstSize.Height > sizeRet.Height) ||
                              (dstSize.Width > sizeRet.Width && dstSize.Height < sizeRet.Height) ||
                              (dstSize.Width * dstSize.Height > sizeRet.Width * sizeRet.Height);

            // 如果计算结果不合理，使用边界差值计算
            if (bWrongSize)
            {
                sizeRet = new Size(
                    (int)(fRightX - fLeftX + 0.5),
                    (int)(fTopY - fBottomY + 0.5)
                );
            }

            return sizeRet;
        }

        /// <summary>
        /// 计算点绕指定中心点旋转后的新坐标
        /// </summary>
        /// <param name="ptInput">输入点坐标</param>
        /// <param name="ptOrg">旋转中心点坐标</param>
        /// <param name="dAngle">旋转角度（弧度）</param>
        /// <returns>旋转后的点坐标</returns>
        private Point2f ptRotatedPt2f(Point2f ptInput, Point2f ptOrg, double dAngle)
        {
            // 计算旋转区域的宽度和高度
            double dWidth = ptOrg.X * 2;
            double dHeight = ptOrg.Y * 2;

            // 计算y坐标的映射
            double dY1 = dHeight - ptInput.Y;
            double dY2 = dHeight - ptOrg.Y;

            // 计算旋转后的x坐标
            double dX = (ptInput.X - ptOrg.X) * Math.Cos(dAngle) -
                (dY1 - ptOrg.Y) * Math.Sin(dAngle) + ptOrg.X;

            // 计算旋转后的y坐标
            double dY = (ptInput.X - ptOrg.X) * Math.Sin(dAngle) +
                        (dY1 - ptOrg.Y) * Math.Cos(dAngle) + dY2;

            // 将y坐标映射回原始坐标系
            dY = -dY + dHeight;

            // 返回旋转后的点坐标
            return new Point2f((float)dX, (float)dY);
        }

        /// <summary>
        /// 可视化模板图像金字塔
        /// </summary>
        public void ShowTemplatePyramid()
        {
            if (TemplateData.IsPatternLearned)
            {
                Console.WriteLine($"金字塔层数: {TemplateData.VecPyramid.Count}");
                // 显示金字塔图像
                foreach (var mat in TemplateData.VecPyramid)
                {
                    Cv2.ImShow("Pyramid Image", mat);
                    Cv2.WaitKey(0);
                }
            }
        }

        /// <summary>
        /// 可视化匹配结果
        /// </summary>
        /// <param name="image">输入彩色图像</param>
        /// <param name="color">边框颜色</param>
        /// <param name="indexFrontColor">索引字体颜色</param>
        /// <param name="scoreFrontColor">得分字体颜色</param>
        /// <param name="frontScale">字体大小</param>
        /// <param name="thickness">字体和线条粗细</param>
        /// <param name="showIndex">是否显示索引值</param>
        /// <param name="showDirection">是否显示方向箭头</param>
        /// <param name="showMark">是否显示匹配中心标记</param>
        /// <param name="showScore">是否显示匹配得分</param>
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
                Point ptLT = new Point(Matches[i].LeftTop.X, Matches[i].LeftTop.Y);
                Point ptRB = new Point(Matches[i].RightBottom.X, Matches[i].RightBottom.Y);
                Point ptLB = new Point(Matches[i].LeftBottom.X, Matches[i].LeftBottom.Y);
                Point ptRT = new Point(Matches[i].RightTop.X, Matches[i].RightTop.Y);

                Matches[i].Visualize(image, color, indexFrontColor, scoreFrontColor,
                    frontScale: frontScale, thickness: thickness, showMark, showDirection, showIndex, showScore);
            }
        }

        /// <summary>
        /// 保存模板数据到文件
        /// </summary>
        /// <param name="filePath">模板数据文件路径</param>
        /// <returns>是否保存成功，true成功，false失败</returns>
        public bool SaveTemplateData(string filePath)
        {
            return TemplateData.Save(filePath);
        }

        /// <summary>
        /// 从文件加载模板数据
        /// </summary>
        /// <param name="filePath">模板数据文件路径</param>
        /// <returns>是否加载成功，true成功，false失败</returns>
        public bool LoadTemplateData(string filePath)
        {
            return TemplateData.Load(filePath);
        }
    }
}
