#include "PatternMatchByNCC.h"

PatternMatch::PatternMatchByNCC::PatternMatchByNCC()
{
    m_templateData = TemplateData();
}

/**
 * 计算图像金字塔的层数
 * @param matTempl 模板图像引用
 * @param minTemplLength 模板图像最小缩放尺寸
 * @return 图像金字塔层数
 */
int PatternMatch::PatternMatchByNCC::GetTopLayer(Mat* matTempl, int minTemplLength)
{
    int iTopLayer = 0;
    int iMinReduceArea = static_cast<int>(std::pow(std::sqrt(minTemplLength), 2));
    int iArea = matTempl->rows * matTempl->cols;
    while (iArea > iMinReduceArea)
    {
        iArea /= 4;  // 每次缩小为原来的1/4
        iTopLayer++;
    }

    return iTopLayer;
}

void PatternMatch::PatternMatchByNCC::Learn_Pattern(const unsigned char* pTemplImageData,
                                                    int iTemplWidth,
                                                    int iTemplHeight,
                                                    int iTemplStride,
                                                    int iPyramidLayers,
                                                    int iMinReduceSize,
                                                    bool bAutoPyramidLayers)
{
    // 检查输入参数
    if (pTemplImageData == nullptr || iTemplWidth <= 0 || iTemplHeight <= 0 || iTemplStride < iTemplWidth)
    {
        throw std::invalid_argument("Invalid template image parameters");
    }

    // 创建一个新的Mat来存储模板图像
    // 注意：使用步长创建Mat，确保内存对齐正确
    m_matTemplImage = Mat(iTemplHeight, iTemplWidth, CV_8UC1);

    // 逐行复制数据，处理stride
    for (int i = 0; i < iTemplHeight; i++)
    {
        // 源数据的行起始位置
        const unsigned char* pSrcRow = pTemplImageData + i * iTemplStride;
        // 目标Mat的行起始位置
        unsigned char* pDstRow = m_matTemplImage.ptr<unsigned char>(i);
        // 复制每一行的有效数据（宽度）
        memcpy(pDstRow, pSrcRow, iTemplWidth);
    }

    // 验证图像是否成功创建
    if (m_matTemplImage.empty())
    {
        throw std::runtime_error("Failed to create template image");
    }

    // 学习模板图像的其他处理逻辑
    m_templateData.Clear();

    if (bAutoPyramidLayers)
    {
        iPyramidLayers = GetTopLayer(&m_matTemplImage, iMinReduceSize);
    }
    else
    {
        iPyramidLayers -= 1;
    }

    // 构建模板图像金字塔
    buildPyramid(m_matTemplImage, m_templateData.m_vecPyramid, iPyramidLayers);
    m_templateData.Resize();
    // 计算模板边界颜色
    m_templateData.m_iBorderColor = mean(m_matTemplImage).val[0] < 128 ? 255 : 0;
    // 计算每层金字塔的统计参数
    for (int i = 0; i < m_templateData.m_iPyramidLayers; i++)
    {
        // 计算面积倒数
        double dInvArea =
            1.0 / (static_cast<double>(m_templateData.m_vecPyramid[i].rows) * m_templateData.m_vecPyramid[i].cols);

        // 计算均值和标准差
        Scalar templMean, templStdDev;
        meanStdDev(m_templateData.m_vecPyramid[i], templMean, templStdDev);

        // 计算标准差的平方和
        double templNorm = templStdDev[0] * templStdDev[0] + templStdDev[1] * templStdDev[1] +
                           templStdDev[2] * templStdDev[2] + templStdDev[3] * templStdDev[3];

        // 检测是否为纯色图像
        if (templNorm < DBL_EPSILON)
        {
            m_templateData.m_vecResultEqual1[i] = true;
        }
        else
        {
            m_templateData.m_vecResultEqual1[i] = false;
        }

        // 计算总平方和
        double templSum = templNorm + templMean[0] * templMean[0] + templMean[1] * templMean[1] +
                          templMean[2] * templMean[2] + templMean[3] * templMean[3];

        templSum /= dInvArea;
        templNorm = std::sqrt(templNorm);
        templNorm /= std::sqrt(dInvArea);

        // 存储计算结果
        m_templateData.m_vecTemplMean[i] = templMean;
        m_templateData.m_vecTemplNorm[i] = templNorm;
        m_templateData.m_vecInvArea[i] = dInvArea;
    }

    m_templateData.m_bIsPatternLearned = true;
}

int PatternMatch::PatternMatchByNCC::GetTemplatePyramidLayers()
{
    if (!m_templateData.m_bIsPatternLearned)
    {
        return 0;  // 模板未学习，返回0层
    }
    else
    {
        return m_templateData.m_iPyramidLayers;  // 返回金字塔层数
    }
}

unsigned char *PatternMatch::PatternMatchByNCC::GetPyramidInfo(
    int index, int* out_rows, int* out_cols, int* out_type, int* out_step)
{
    if (index < 0 || index >= m_templateData.m_iPyramidLayers)
    {
        throw std::out_of_range("Index out of range for pyramid layers");
    }
    const Mat& pyramidLayer = m_templateData.m_vecPyramid[index];
    *out_rows = pyramidLayer.rows;
    *out_cols = pyramidLayer.cols;
    *out_type = pyramidLayer.type();
    *out_step = static_cast<int>(pyramidLayer.step);  // 获取行步长

    return pyramidLayer.data;  // 返回数据指针
}

/**
 * @brief 计算点绕中心点旋转后的新坐标
 * @param ptInput 输入点
 * @param ptOrg 旋转中心
 * @param dAngle 旋转角度（弧度制）
 * @return 旋转后的点
 */
Point2f PatternMatch::PatternMatchByNCC::ptRotatePt2f(Point2f ptInput, Point2f ptOrg, double dAngle)
{
    double dWidth = ptOrg.x * 2;
    double dHeight = ptOrg.y * 2;
    double dY1 = dHeight - ptInput.y;
    double dY2 = dHeight - ptOrg.y;

    double dX = (ptInput.x - ptOrg.x) * cos(dAngle) - (dY1 - ptOrg.y) * sin(dAngle) + ptOrg.x;
    double dY = (ptInput.x - ptOrg.x) * sin(dAngle) + (dY1 - ptOrg.y) * cos(dAngle) + dY2;

    dY = -dY + dHeight;  // 调整Y坐标，使其符合OpenCV的坐标系
    return Point2f(static_cast<float>(dX), static_cast<float>(dY));
}

/**
 * @brief 获取最佳旋转图像尺寸
 * @param sizeSrc 源图像尺寸
 * @param sizeDst 模板图像尺寸
 * @param dRAngle 旋转角度
 * @return 最佳旋转图像尺寸
 */
Size PatternMatch::PatternMatchByNCC::GetBestRotationSize(Size sizeSrc, Size sizeDst, double dRAngle)
{
    double dRAngle_radian = dRAngle * D2R;
    Point ptLT(0, 0);
    Point ptRB(sizeSrc.width - 1, sizeSrc.height - 1);
    Point ptLB(0, sizeSrc.height - 1);
    Point ptRT(sizeSrc.width - 1, 0);
    Point2f ptCenter((sizeSrc.width - 1) / 2.0f, (sizeSrc.height - 1) / 2.0f);
    // 计算旋转后的四个角点
    Point2f ptLT_R = ptRotatePt2f(Point2f(ptLT), ptCenter, dRAngle_radian);
    Point2f ptLB_R = ptRotatePt2f(Point2f(ptLB), ptCenter, dRAngle_radian);
    Point2f ptRB_R = ptRotatePt2f(Point2f(ptRB), ptCenter, dRAngle_radian);
    Point2f ptRT_R = ptRotatePt2f(Point2f(ptRT), ptCenter, dRAngle_radian);

    float fTopY = max(max(ptLT_R.y, ptLB_R.y), max(ptRB_R.y, ptRT_R.y));
    float fBottomY = min(min(ptLT_R.y, ptLB_R.y), min(ptRB_R.y, ptRT_R.y));
    float fRightX = max(max(ptLT_R.x, ptLB_R.x), max(ptRB_R.x, ptRT_R.x));
    float fLeftX = min(min(ptLT_R.x, ptLB_R.x), min(ptRB_R.x, ptRT_R.x));

    if (dRAngle > 360)
        dRAngle -= 360;
    else if (dRAngle < 0)
        dRAngle += 360;

    if (fabs(fabs(dRAngle) - 90) < VISION_TOLERANCE || fabs(fabs(dRAngle) - 270) < VISION_TOLERANCE)
    {
        return Size(sizeSrc.height, sizeSrc.width);
    }
    else if (fabs(dRAngle) < VISION_TOLERANCE || fabs(fabs(dRAngle) - 180) < VISION_TOLERANCE)
    {
        return sizeSrc;
    }

    double dAngle = dRAngle;

    if (dAngle > 0 && dAngle < 90)
    {
        ;
    }
    else if (dAngle > 90 && dAngle < 180)
    {
        dAngle -= 90;
    }
    else if (dAngle > 180 && dAngle < 270)
    {
        dAngle -= 180;
    }
    else if (dAngle > 270 && dAngle < 360)
    {
        dAngle -= 270;
    }
    else  // Debug
    {
        throw std::invalid_argument("Invalid angle value: " + std::to_string(dAngle));
    }

    double fH1 = sizeDst.width * sin(dAngle * D2R) * cos(dAngle * D2R);
    double fH2 = sizeDst.height * sin(dAngle * D2R) * cos(dAngle * D2R);

    int iHalfHeight = static_cast<int>(ceil(fTopY - ptCenter.y - fH1));
    int iHalfWidth = static_cast<int>(ceil(fRightX - ptCenter.x - fH2));

    Size sizeRet(iHalfWidth * 2, iHalfHeight * 2);

    bool bWrongSize =
        (sizeDst.width < sizeRet.width && sizeDst.height > sizeRet.height) ||
        (sizeDst.width > sizeRet.width && sizeDst.height < sizeRet.height || sizeDst.area() > sizeRet.area());
    if (bWrongSize)
        sizeRet = Size(int(fRightX - fLeftX + 0.5), int(fTopY - fBottomY + 0.5));

    return sizeRet;
}

/**
 * @brief 使用SSE指令集加速的模板匹配卷积运算
 * @param pCharKernel 模板图像数据
 * @param pCharConv 待匹配图像数据
 * @param iLength 数据长度
 * @return 卷积结果
 */
int PatternMatch::PatternMatchByNCC::IM_Conv_SIMD(const unsigned char* pCharKernel,
                                                  const unsigned char* pCharConv,
                                                  int iLength)
{
    const int iBlockSize = 16;
    int Block = iLength / iBlockSize;
    __m128i SumV = _mm_setzero_si128();
    __m128i Zero = _mm_setzero_si128();

    for (int Y = 0; Y < Block * iBlockSize; Y += iBlockSize)
    {
        __m128i SrcK = _mm_loadu_si128((__m128i*)(pCharKernel + Y));
        __m128i SrcC = _mm_loadu_si128((__m128i*)(pCharConv + Y));
        __m128i SrcK_L = _mm_unpacklo_epi8(SrcK, Zero);
        __m128i SrcK_H = _mm_unpackhi_epi8(SrcK, Zero);
        __m128i SrcC_L = _mm_unpacklo_epi8(SrcC, Zero);
        __m128i SrcC_H = _mm_unpackhi_epi8(SrcC, Zero);
        __m128i SumT = _mm_add_epi32(_mm_madd_epi16(SrcK_L, SrcC_L), _mm_madd_epi16(SrcK_H, SrcC_H));
        SumV = _mm_add_epi32(SumV, SumT);
    }

    // Horizontal sum
    int temp[4];
    _mm_storeu_si128((__m128i*)temp, SumV);
    int sum = temp[0] + temp[1] + temp[2] + temp[3];

    for (int Y = Block * iBlockSize; Y < iLength; Y++)
    {
        sum += pCharKernel[Y] * pCharConv[Y];
    }

    return sum;
}

/**
 * @brief 计算归一化系数
 * @param matSrc 源图像Mat对象
 * @param pTemplateData 模板数据指针
 * @param matResult 匹配结果Mat对象
 * @param iLayer 当前金字塔层级
 */
void PatternMatch::PatternMatchByNCC::CCOEFF_Denominator(const Mat& matSrc,
                                                         TemplateData* pTemplateData,
                                                         Mat& matResult,
                                                         int iLayer)
{
    // 纯色图像特殊处理
    if (pTemplateData->m_vecResultEqual1[iLayer])
    {
        matResult = Scalar::all(1);
        return;
    }
    double *q0 = 0, *q1 = 0, *q2 = 0, *q3 = 0;

    // 计算积分图
    Mat sum, sqsum;
    integral(matSrc, sum, sqsum, CV_64F);

    q0 = (double*)sqsum.data;
    q1 = q0 + pTemplateData->m_vecPyramid[iLayer].cols;
    q2 = (double*)(sqsum.data + pTemplateData->m_vecPyramid[iLayer].rows * sqsum.step);
    q3 = q2 + pTemplateData->m_vecPyramid[iLayer].cols;

    double* p0 = (double*)sum.data;
    double* p1 = p0 + pTemplateData->m_vecPyramid[iLayer].cols;
    double* p2 = (double*)(sum.data + pTemplateData->m_vecPyramid[iLayer].rows * sum.step);
    double* p3 = p2 + pTemplateData->m_vecPyramid[iLayer].cols;

    int sumstep = sum.data ? (int)(sum.step / sizeof(double)) : 0;
    int sqstep = sqsum.data ? (int)(sqsum.step / sizeof(double)) : 0;

    //
    double dTemplMean0 = pTemplateData->m_vecTemplMean[iLayer][0];
    double dTemplNorm = pTemplateData->m_vecTemplNorm[iLayer];
    double dInvArea = pTemplateData->m_vecInvArea[iLayer];
    //

    int i, j;
    for (i = 0; i < matResult.rows; i++)
    {
        float* rrow = matResult.ptr<float>(i);
        int idx = i * sumstep;
        int idx2 = i * sqstep;

        for (j = 0; j < matResult.cols; j += 1, idx += 1, idx2 += 1)
        {
            double num = rrow[j], t;
            double wndMean2 = 0, wndSum2 = 0;

            t = p0[idx] - p1[idx] - p2[idx] + p3[idx];
            wndMean2 += t * t;
            num -= t * dTemplMean0;
            wndMean2 *= dInvArea;


            t = q0[idx2] - q1[idx2] - q2[idx2] + q3[idx2];
            wndSum2 += t;


            // t = std::sqrt (MAX (wndSum2 - wndMean2, 0)) * dTemplNorm;

            double diff2 = MAX(wndSum2 - wndMean2, 0);
            if (diff2 <= std::min(0.5, 10 * FLT_EPSILON * wndSum2))
                t = 0;  // avoid rounding errors
            else
                t = std::sqrt(diff2) * dTemplNorm;

            if (fabs(num) < t)
                num /= t;
            else if (fabs(num) < t * 1.125)
                num = num > 0 ? 1 : -1;
            else
                num = 0;

            rrow[j] = (float)num;
        }
    }
}

/**
 * @brief 模板匹配函数
 * @param matSrc 源图像Mat对象
 * @param pTemplateData 模板数据指针
 * @param matResult 匹配结果Mat对象
 * @param iLayer 当前金字塔层级
 * @param bUseSIMD 是否使用SIMD加速
 */
void PatternMatch::PatternMatchByNCC::MatchTemplate(
    const Mat& matSrc, TemplateData* pTemplateData, Mat& matResult, int iLayer, bool bUseSIMD)
{
    if (bUseSIMD) 
    {
        // 创建结果矩阵
        // From ImageShop
        matResult.create(matSrc.rows - pTemplateData->m_vecPyramid[iLayer].rows + 1,
                         matSrc.cols - pTemplateData->m_vecPyramid[iLayer].cols + 1, CV_32FC1);
        matResult.setTo(0);
        cv::Mat& matTemplate = pTemplateData->m_vecPyramid[iLayer];

        // 逐行计算相关系数
        int t_r_end = matTemplate.rows, t_r = 0;
        for (int r = 0; r < matResult.rows; r++)
        {
            float* r_matResult = matResult.ptr<float>(r);
            const unsigned char*r_source = matSrc.ptr<uchar>(r);
            const uchar *r_template, *r_sub_source;
            for (int c = 0; c < matResult.cols; ++c, ++r_matResult, ++r_source)
            {
                r_template = matTemplate.ptr<uchar>();
                r_sub_source = r_source;
                // 使用SIMD加速的卷积计算
                for (t_r = 0; t_r < t_r_end; ++t_r, r_sub_source += matSrc.cols, r_template += matTemplate.cols)
                {
                    *r_matResult = *r_matResult + IM_Conv_SIMD(r_template, r_sub_source, matTemplate.cols);
                }
            }
        }
        // From ImageShop
    }
    else 
    {
        // 使用OpenCV的matchTemplate函数进行模板匹配
        matchTemplate(matSrc, pTemplateData->m_vecPyramid[iLayer], matResult, CV_TM_CCORR);
    }

    // 计算归一化系数
    CCOEFF_Denominator(matSrc, pTemplateData, matResult, iLayer);
}

/**
 * @brief 获取下一个最大位置
 * @param matResult 匹配结果矩阵
 * @param ptMaxLoc 当前最大位置
 * @param sizeTemplate 模板尺寸
 * @param dMaxValue 最大值输出参数
 * @param dMaxOverlap 最大重叠率
 * @param blockMax 匹配块最大值对象
 * @return 下一个最大位置
 */
Point PatternMatch::PatternMatchByNCC::GetNextMaxLoc(
    const Mat& matResult, Point ptMaxLoc, Size sizeTemplate, double& dMaxValue, double dMaxOverlap, BlockMax& blockMax)
{
    // 计算忽略区域
    // 比對到的區域需考慮重疊比例
    int iStartX = int(ptMaxLoc.x - sizeTemplate.width * (1 - dMaxOverlap));
    int iStartY = int(ptMaxLoc.y - sizeTemplate.height * (1 - dMaxOverlap));
    Rect rectIgnore(iStartX, iStartY, int(2 * sizeTemplate.width * (1 - dMaxOverlap)),
                    int(2 * sizeTemplate.height * (1 - dMaxOverlap)));
    // 在结果矩阵中填充忽略区域
    // 塗黑
    rectangle(matResult, rectIgnore, Scalar(-1), CV_FILLED);
    blockMax.UpdateMax(rectIgnore);
    // 获取下一个最大值位置
    Point ptReturn;
    blockMax.GetMaxValueLoc(dMaxValue, ptReturn);
    return ptReturn;
}

/**
 * @brief 获取下一个最大位置
 * @param matResult 匹配结果矩阵
 * @param ptMaxLoc 当前最大位置
 * @param sizeTemplate 模板尺寸
 * @param dMaxValue 最大值输出参数
 * @param dMaxOverlap 最大重叠率
 * @return 下一个最大位置
 */
Point PatternMatch::PatternMatchByNCC::GetNextMaxLoc(
    const Mat& matResult, Point ptMaxLoc, Size sizeTemplate, double& dMaxValue, double dMaxOverlap)
{
    int iStartX = static_cast<int>(ptMaxLoc.x - sizeTemplate.width * (1 - dMaxOverlap));
    int iStartY = static_cast<int>(ptMaxLoc.y - sizeTemplate.height * (1 - dMaxOverlap));
    // 塗黑
    rectangle(matResult,
              Rect(iStartX, iStartY, static_cast<int>(2 * sizeTemplate.width * (1 - dMaxOverlap)),
                   static_cast<int>(2 * sizeTemplate.height * (1 - dMaxOverlap))),
              Scalar(-1), CV_FILLED);
    // 得到下一個最大值
    Point ptNewMaxLoc;
    minMaxLoc(matResult, 0, &dMaxValue, 0, &ptNewMaxLoc);
    return ptNewMaxLoc;
}

/**
 * @brief 获取旋转后的ROI区域
 * @param matSrc 源图像Mat对象
 * @param size 旋转后的尺寸
 * @param ptLT 左上角点
 * @param dAngle 旋转角度
 * @param matRoi ROI区域Mat对象
 */
void PatternMatch::PatternMatchByNCC::GetRotatedROI(
    Mat& matSrc, Size size, Point2f ptLT, double dAngle, Mat& matRoi)
{
    double dAngle_radian = dAngle * D2R;
    Point2f ptC((matSrc.cols - 1) / 2.0f, (matSrc.rows - 1) / 2.0f);
    Point2f ptLT_rotate = ptRotatePt2f(ptLT, ptC, dAngle_radian);
    Size sizePadding(size.width + 6, size.height + 6);


    Mat rMat = getRotationMatrix2D(ptC, dAngle, 1);
    rMat.at<double>(0, 2) -= ptLT_rotate.x - 3;
    rMat.at<double>(1, 2) -= ptLT_rotate.y - 3;
    // 平移旋轉矩陣(0, 2) (1, 2)的減，為旋轉後的圖形偏移，-= ptLT_rotate.x - 3 代表旋轉後的圖形往-X方向移動ptLT_rotate.x
    // - 3 Debug

    // Debug
    warpAffine(matSrc, matRoi, rMat, sizePadding);
}
bool PatternMatch::PatternMatchByNCC::SubPixEsimation(
    vector<MatchParameter>* vec, double* dNewX, double* dNewY, double* dNewAngle, double dAngleStep, int iMaxScoreIndex)
{
    // 构建最小二乘法方程 Az=S
    // Az=S, (A.T)Az=(A.T)s, z = ((A.T)A).inv (A.T)s

    Mat matA(27, 10, CV_64F);  // 系数矩阵A
    Mat matZ(10, 1, CV_64F);   // 待求解参数Z
    Mat matS(27, 1, CV_64F);   // 常数项S

    // 获取最高分数点的坐标和角度
    double dX_maxScore = (*vec)[iMaxScoreIndex].m_pt.x;
    double dY_maxScore = (*vec)[iMaxScoreIndex].m_pt.y;
    double dTheata_maxScore = (*vec)[iMaxScoreIndex].m_dMatchAngle;

    // 构建方程组
    int iRow = 0;
    for (int theta = 0; theta <= 2; theta++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                // 矩阵A的各项系数:xx yy tt xy xt yt x y t 1
                // xx yy tt xy xt yt x y t 1
                // 0  1  2  3  4  5  6 7 8 9
                double dX = dX_maxScore + x;
                double dY = dY_maxScore + y;
                double dT = (dTheata_maxScore + (theta - 1) * dAngleStep) * D2R;
                // 填充矩阵A的一行
                matA.at<double>(iRow, 0) = dX * dX;
                matA.at<double>(iRow, 1) = dY * dY;
                matA.at<double>(iRow, 2) = dT * dT;
                matA.at<double>(iRow, 3) = dX * dY;
                matA.at<double>(iRow, 4) = dX * dT;
                matA.at<double>(iRow, 5) = dY * dT;
                matA.at<double>(iRow, 6) = dX;
                matA.at<double>(iRow, 7) = dY;
                matA.at<double>(iRow, 8) = dT;
                matA.at<double>(iRow, 9) = 1.0;
                // 填充矩阵S
                matS.at<double>(iRow, 0) = (*vec)[iMaxScoreIndex + (theta - 1)].m_vecResult[x + 1][y + 1];
                iRow++;
            }
        }
    }

    // 求解方程组获得系数矩阵Z
    // 求解Z矩陣，得到k0~k9
    //[ x* ] = [ 2k0 k3 k4 ]-1 [ -k6 ]
    //| y* | = | k3 2k1 k5 |   | -k7 |
    //[ t* ] = [ k4 k5 2k2 ]   [ -k8 ]

    // solve (matA, matS, matZ, DECOMP_SVD);
    matZ = (matA.t() * matA).inv() * matA.t() * matS;
    // 将Z转置便于访问
    Mat matZ_t;
    transpose(matZ, matZ_t);
    double* dZ = matZ_t.ptr<double>(0);
    // 构建用于求取精确位置的矩阵
    Mat matK1 = (Mat_<double>(3, 3) << (2 * dZ[0]), dZ[3], dZ[4], dZ[3], (2 * dZ[1]), dZ[5], dZ[4], dZ[5], (2 * dZ[2]));
    Mat matK2 = (Mat_<double>(3, 1) << -dZ[6], -dZ[7], -dZ[8]);
    // 求解精确位置和角度
    Mat matDelta = matK1.inv() * matK2;

    // 输出结果
    *dNewX = matDelta.at<double>(0, 0);
    *dNewY = matDelta.at<double>(1, 0);
    *dNewAngle = matDelta.at<double>(2, 0) * R2D;
    return true;
}

/**
 * @brief 根据匹配分数过滤结果
 * @param vec 匹配参数向量
 * @param dScore 分数阈值
 */
void PatternMatch::PatternMatchByNCC::FilterWithScore(vector<MatchParameter>* vec, double dScore)
{
    sort(vec->begin(), vec->end(),
         [](const MatchParameter& a, const MatchParameter& b) { return a.m_dMatchScore > b.m_dMatchScore; });
    int iSize = static_cast<int>(vec->size());
    int iIndexDelete = iSize + 1;
    for (int i = 0; i < iSize; i++)
    {
        if ((*vec)[i].m_dMatchScore < dScore)
        {
            iIndexDelete = i;
            break;
        }
    }
    if (iIndexDelete == iSize + 1)  // 沒有任何元素小於dScore
        return;
    vec->erase(vec->begin() + iIndexDelete, vec->end());
    return;
}

/**
 * @brief 根据中心点对点进行排序
 * @param vecSort 待排序的点向量
 */
void PatternMatch::PatternMatchByNCC::SortPtWithCenter(vector<Point_<float>>& vecSort)
{  // 计算中心点
    int iSize = (int)vecSort.size();
    Point2f ptCenter;
    for (int i = 0; i < iSize; i++)
        ptCenter += vecSort[i];
    ptCenter /= iSize;

    Point2f vecX(1, 0);

    // 计算每个点相对于中心点的角度
    vector<pair<Point2f, double>> vecPtAngle(iSize);
    for (int i = 0; i < iSize; i++)
    {
        vecPtAngle[i].first = vecSort[i];  // pt
        Point2f vec1(vecSort[i].x - ptCenter.x, vecSort[i].y - ptCenter.y);
        float fNormVec1 = vec1.x * vec1.x + vec1.y * vec1.y;
        float fDot = vec1.x;

        if (vec1.y < 0)  // 若點在中心的上方
        {
            vecPtAngle[i].second = acos(fDot / fNormVec1) * R2D;
        }
        else if (vec1.y > 0)  // 下方
        {
            vecPtAngle[i].second = 360 - acos(fDot / fNormVec1) * R2D;
        }
        else  // 點與中心在相同Y
        {
            if (vec1.x - ptCenter.x > 0)
                vecPtAngle[i].second = 0;
            else
                vecPtAngle[i].second = 180;
        }
    }
    sort(vecPtAngle.begin(), vecPtAngle.end(),
         [](const pair<Point2f, double>& a, const pair<Point2f, double>& b) { return a.second < b.second; });
    for (int i = 0; i < iSize; i++)
    {
        vecSort[i] = vecPtAngle[i].first;
    }
}

/**
 * @brief 根据旋转矩形过滤匹配结果
 * @param vec 匹配参数向量
 * @param iMethod 匹配方法
 * @param dMaxOverLap 最大重叠率
 */
void PatternMatch::PatternMatchByNCC::FilterWithRotatedRect(vector<MatchParameter>* vec,
                                                            int iMethod,
                                                            double dMaxOverLap)
{
    int iMatchSize = (int)vec->size();
    RotatedRect rect1, rect2;
    for (int i = 0; i < iMatchSize - 1; i++)
    {
        if (vec->at(i).m_bDelete)
            continue;
        for (int j = i + 1; j < iMatchSize; j++)
        {
            if (vec->at(j).m_bDelete)
                continue;
            rect1 = vec->at(i).m_rectR;
            rect2 = vec->at(j).m_rectR;
            vector<Point2f> vecInterSec;
            int iInterSecType = rotatedRectangleIntersection(rect1, rect2, vecInterSec);
            if (iInterSecType == INTERSECT_NONE)  // 無交集
                continue;
            else if (iInterSecType == INTERSECT_FULL)  // 一個矩形包覆另一個
            {
                int iDeleteIndex;
                if (iMethod == CV_TM_SQDIFF)
                    iDeleteIndex = (vec->at(i).m_dMatchScore <= vec->at(j).m_dMatchScore) ? j : i;
                else
                    iDeleteIndex = (vec->at(i).m_dMatchScore >= vec->at(j).m_dMatchScore) ? j : i;
                vec->at(iDeleteIndex).m_bDelete = true;
            }
            else  // 交點 > 0
            {
                if (vecInterSec.size() < 3)  // 一個或兩個交點
                    continue;
                else
                {
                    int iDeleteIndex;
                    // 求面積與交疊比例
                    SortPtWithCenter(vecInterSec);
                    double dArea = contourArea(vecInterSec);
                    double dRatio = dArea / rect1.size.area();
                    // 若大於最大交疊比例，選分數高的
                    if (dRatio > dMaxOverLap)
                    {
                        if (iMethod == CV_TM_SQDIFF)
                            iDeleteIndex = (vec->at(i).m_dMatchScore <= vec->at(j).m_dMatchScore) ? j : i;
                        else
                            iDeleteIndex = (vec->at(i).m_dMatchScore >= vec->at(j).m_dMatchScore) ? j : i;
                        vec->at(iDeleteIndex).m_bDelete = true;
                    }
                }
            }
        }
    }
    vector<MatchParameter>::iterator it;
    for (it = vec->begin(); it != vec->end();)
    {
        if ((*it).m_bDelete)
            it = vec->erase(it);
        else
            ++it;
    }
}

/**
 * @brief 目标匹配
 * @param srcImageData 源图像数据指针
 * @param srcImageWidth 源图像宽度
 * @param srcImageHeight 源图像高度
 * @param srcImageStride 源图像行步长
 * @param out_array 匹配结果输出数组指针
 * @param src_reverse 是否启用源图像反转
 * @param angle_step 匹配旋转角度步长
 * @param auto_angle_step 是否启用自动计算旋转角度步长
 * @param start_angle 匹配起始角度
 * @param angle_range 匹配角度范围
 * @param match_threshold 匹配阈值
 * @param max_match_count 最大匹配目标数量
 * @param use_simd 是否启用SIMD加速
 * @param max_overlap 匹配目标最大重叠率
 * @param sub_pixel_estimation 是否启用亚像素级估计
 * @param fast_mode 是否启用快速匹配模式
 * @param debug 是否启用调试模式
 * @return 实际匹配目标数量
 */
int PatternMatch::PatternMatchByNCC::Match(const unsigned char* srcImageData,
                                           int srcImageWidth,
                                           int srcImageHeight,
                                           int srcImageStride,
                                           SingleTargetMatch* out_array,
                                           bool src_reverse,
                                           double angle_step,
                                           bool auto_angle_step,
                                           double start_angle,
                                           double angle_range,
                                           double match_threshold,
                                           int max_match_count,
                                           bool use_simd,
                                           double max_overlap,
                                           bool sub_pixel_estimation,
                                           bool fast_mode,
                                           bool debug)
{
    // 检查模板是否已学习
    if (!m_templateData.m_bIsPatternLearned)
    {
        throw std::runtime_error("Template pattern has not been learned yet.");
    }
    // 检查输入参数
    if (srcImageData == nullptr || srcImageWidth <= 0 || srcImageHeight <= 0 || srcImageStride < srcImageWidth)
    {
        throw std::invalid_argument("Invalid source image parameters");
    }
    // 创建源图像Mat对象，使用步长确保内存对齐正确
    Mat matSrc(srcImageHeight, srcImageWidth, CV_8UC1, (void*)srcImageData, srcImageStride);
    // 验证源图像是否为空
    if (matSrc.empty() || m_matTemplImage.empty())
    {
        throw std::runtime_error("Source image or template image is empty.");
    }
    // 验证模板尺寸是否合理
    if (m_matTemplImage.cols > matSrc.cols || m_matTemplImage.rows > matSrc.rows)
    {
        throw std::invalid_argument("Template size is larger than source image");
    }

    // 验证尺寸比例是否合理（避免一边大一边小的情况）
    if ((m_matTemplImage.cols < matSrc.cols && m_matTemplImage.rows > matSrc.rows) ||
        (m_matTemplImage.cols > matSrc.cols && m_matTemplImage.rows < matSrc.rows))
    {
        throw std::invalid_argument("Invalid template and source image size ratio");
    }

    // 验证面积比例
    if (m_matTemplImage.cols * m_matTemplImage.rows > matSrc.cols * matSrc.rows)
    {
        throw std::invalid_argument("Template area is larger than source image");
    }

    imwrite("src.png", matSrc);
    
    // 建立源图像图像金字塔
    int iTopLayer = m_templateData.m_iPyramidLayers - 1;
    vector<Mat> vecMatSrcPyr;
    if (src_reverse)
    {
        Mat matNewSrc = 255 - matSrc;
        buildPyramid(matNewSrc, vecMatSrcPyr, iTopLayer);
    }
    else 
    {
        buildPyramid(matSrc, vecMatSrcPyr, iTopLayer);
    }

    // 第一阶段粗匹配
    TemplateData* pTemplateData = &m_templateData;
    // 计算旋转角度步长
    double dAngleStep = angle_step;
    if (auto_angle_step)
    {
        dAngleStep =
            atan(2.0 / max(pTemplateData->m_vecPyramid[iTopLayer].cols, pTemplateData->m_vecPyramid[iTopLayer].rows)) *
            R2D;
    }
    else
    {
        angle_step = std::max(angle_step, 1.0);  // 确保角度步长不小于1度
    }
    vector<double> vecAngles;
    // if (angle_range > 0)
    // {
    //     // 逆时针匹配
    //     for (double angle = start_angle; angle < start_angle + angle_range + dAngleStep; angle += dAngleStep)
    //     {
    //         vecAngles.push_back(angle);
    //     }
    // }
    // else
    // {
    //     if (abs(start_angle) < VISION_TOLERANCE) 
    //     {
    //         vecAngles.push_back(start_angle);  // 如果起始角度为0，则只添加0度
    //     }
    //     else 
    //     {
    //         // 顺时针匹配
    //         for (int i = -1; i <= 1; i++)
    //         {
    //             vecAngles.push_back(start_angle + dAngleStep * i);
    //         }
    //     }
    // }
    if (angle_range > 0)
    {
        // 逆时针匹配
        for (double angle = start_angle; angle < start_angle + angle_range + dAngleStep; angle += dAngleStep)
        {
            vecAngles.push_back(angle);
        }
    }
    else
    {
        vecAngles.push_back(start_angle);
    }

    // 源图像顶层金字塔图像尺寸
    int iTopSrcW = vecMatSrcPyr[iTopLayer].cols;
    int iTopSrcH = vecMatSrcPyr[iTopLayer].rows;
    Point2f ptCenter((iTopSrcW - 1) / 2.0f, (iTopSrcH - 1) / 2.0f);
    int iSize = static_cast<int>(vecAngles.size());
    vector<MatchParameter> vecMatchParameter;

    // 初始化每层金字塔得分阈值
    vector<double> vecLayerScores(iTopLayer + 1);
    double dBaseScore = match_threshold;
    for (int iLayer = 0; iLayer < static_cast<int>(vecLayerScores.size()); iLayer++) 
    {
        vecLayerScores[iLayer] = dBaseScore;
        dBaseScore *= 0.9;  // 每层分数降低10%
    }

    Size sizePat = pTemplateData->m_vecPyramid[iTopLayer].size();
    bool bCalMaxByBlock = (vecMatSrcPyr[iTopLayer].size().area() / sizePat.area() > 500) && max_match_count > 10;
    for (int i = 0; i < iSize; i++)
    {
        Mat matRotatedSrc;
        Mat matR = getRotationMatrix2D(ptCenter, vecAngles[i], 1);
        Mat matResult;
        Point ptMaxLoc;
        double dMaxVal = 0.0;
        double dValue = 0.0;
        Size sizeBest = GetBestRotationSize(vecMatSrcPyr[iTopLayer].size(),
                                            pTemplateData->m_vecPyramid[iTopLayer].size(), vecAngles[i]);

        float fTranslationX = (sizeBest.width - 1) / 2.0f - ptCenter.x;
        float fTranslationY = (sizeBest.height - 1) / 2.0f - ptCenter.y;
        matR.at<double>(0, 2) += fTranslationX;
        matR.at<double>(1, 2) += fTranslationY;
        if (debug)
        {
            string strMat;
            strMat = format("Rotation Matrix: \r\n%.2f, %.2f, %.2f \r\n%.2f, %.2f, %.2f", matR.at<double>(0, 0),
                            matR.at<double>(0, 1), matR.at<double>(0, 2), matR.at<double>(1, 0), matR.at<double>(1, 1),
                            matR.at<double>(1, 2));
            cout << strMat << endl;
        }

        warpAffine(vecMatSrcPyr[iTopLayer], matRotatedSrc, matR, sizeBest, INTER_LINEAR, BORDER_CONSTANT,
                   Scalar(pTemplateData->m_iBorderColor));

        MatchTemplate(matRotatedSrc, pTemplateData, matResult, iTopLayer, false);

        if (bCalMaxByBlock)
        {
            BlockMax blockMax(matResult, pTemplateData->m_vecPyramid[iTopLayer].size());
            blockMax.GetMaxValueLoc(dMaxVal, ptMaxLoc);
            if (dMaxVal < vecLayerScores[iTopLayer])
            {
                continue;  // 如果最大值小于当前层的分数阈值，则跳过
            }
            vecMatchParameter.push_back(
                MatchParameter(Point2f(ptMaxLoc.x - fTranslationX, ptMaxLoc.y - fTranslationY), dMaxVal, vecAngles[i]));
            for (int j = 0; j < max_match_count + MATCH_CANDIDATE_NUM - 1; j++)
            {
                ptMaxLoc = GetNextMaxLoc(matResult, ptMaxLoc, pTemplateData->m_vecPyramid[iTopLayer].size(), dValue,
                                         max_overlap, blockMax);
                if (dValue < vecLayerScores[iTopLayer])
                {
                    break;  // 如果下一个最大值小于当前层的分数阈值，则跳出循环
                }
                vecMatchParameter.push_back(MatchParameter(
                    Point2f(ptMaxLoc.x - fTranslationX, ptMaxLoc.y - fTranslationY), dValue, vecAngles[i]));
            }
        }
        else
        {
            minMaxLoc(matResult, 0, &dMaxVal, 0, &ptMaxLoc);
            if (dMaxVal < vecLayerScores[iTopLayer])
            {
                continue;  // 如果最大值小于当前层的分数阈值，则跳过
            }
            vecMatchParameter.push_back(
                MatchParameter(Point2f(ptMaxLoc.x - fTranslationX, ptMaxLoc.y - fTranslationY), dMaxVal, vecAngles[i]));
            for (int j = 0; j < max_match_count + MATCH_CANDIDATE_NUM - 1; j++)
            {
                ptMaxLoc = GetNextMaxLoc(matResult, ptMaxLoc, pTemplateData->m_vecPyramid[iTopLayer].size(), dValue,
                                         max_overlap);
                if (dValue < vecLayerScores[iTopLayer])
                {
                    break;  // 如果下一个最大值小于当前层的分数阈值，则跳出循环
                }
                vecMatchParameter.push_back(MatchParameter(
                    Point2f(ptMaxLoc.x - fTranslationX, ptMaxLoc.y - fTranslationY), dValue, vecAngles[i]));
            }
        }
    }
    sort(vecMatchParameter.begin(), vecMatchParameter.end(),
         [](const MatchParameter& a, const MatchParameter& b) { return a.m_dMatchScore > b.m_dMatchScore; });

    int iMatchSize = static_cast<int>(vecMatchParameter.size());
    int iDstW = pTemplateData->m_vecPyramid[iTopLayer].cols;
    int iDstH = pTemplateData->m_vecPyramid[iTopLayer].rows;
    // 第一阶段结果可视化
    if (debug)
    {
        int iDebugScale = 2;

        Mat matShow, matResize;
        resize(vecMatSrcPyr[iTopLayer], matResize, vecMatSrcPyr[iTopLayer].size() * iDebugScale);
        cvtColor(matResize, matShow, CV_GRAY2BGR);
        string str = format("Toplayer, Candidate:%d", iMatchSize);
        vector<Point2f> vec;
        for (int i = 0; i < iMatchSize; i++)
        {
            Point2f ptLT, ptRT, ptRB, ptLB;
            double dRAngle = -vecMatchParameter[i].m_dMatchAngle * D2R;
            ptLT = ptRotatePt2f(vecMatchParameter[i].m_pt, ptCenter, dRAngle);
            ptRT = Point2f(ptLT.x + iDstW * (float)cos(dRAngle), ptLT.y - iDstW * (float)sin(dRAngle));
            ptLB = Point2f(ptLT.x + iDstH * (float)sin(dRAngle), ptLT.y + iDstH * (float)cos(dRAngle));
            ptRB = Point2f(ptRT.x + iDstH * (float)sin(dRAngle), ptRT.y + iDstH * (float)cos(dRAngle));
            line(matShow, ptLT * iDebugScale, ptLB * iDebugScale, Scalar(0, 255, 0));
            line(matShow, ptLB * iDebugScale, ptRB * iDebugScale, Scalar(0, 255, 0));
            line(matShow, ptRB * iDebugScale, ptRT * iDebugScale, Scalar(0, 255, 0));
            line(matShow, ptRT * iDebugScale, ptLT * iDebugScale, Scalar(0, 255, 0));
            circle(matShow, ptLT * iDebugScale, 1, Scalar(0, 0, 255));
            vec.push_back(ptLT * iDebugScale);
            vec.push_back(ptRT * iDebugScale);
            vec.push_back(ptLB * iDebugScale);
            vec.push_back(ptRB * iDebugScale);

            string strText = format("%d", i);
            putText(matShow, strText, ptLT * iDebugScale, FONT_HERSHEY_PLAIN, 1, Scalar(0, 255, 0));
        }
        
        imwrite("first_result_" + to_string(iMatchSize) + ".png", matShow);
    }

    // 第二阶段
    int iStopLayer = fast_mode ? 1 : 0;     // 设置为1时，粗匹配，牺牲精度提升速度
    vector<MatchParameter> vecAllResult;
    for (int i = 0; i < vecMatchParameter.size(); i++)
    {
        double dAngle = -vecMatchParameter[i].m_dMatchAngle * D2R;
        Point2f ptLT = ptRotatePt2f(vecMatchParameter[i].m_pt, ptCenter, dAngle);

        double dAngleStep = angle_step;
        if (auto_angle_step)
        {
            dAngleStep = atan(2.0 / max(iDstW, iDstH)) * R2D;
        }
        vecMatchParameter[i].m_dAngleStart = vecMatchParameter[i].m_dMatchAngle - dAngleStep;
        vecMatchParameter[i].m_dAngleEnd = vecMatchParameter[i].m_dMatchAngle + dAngleStep;

        if (iTopLayer <= iStopLayer)
        {
            vecMatchParameter[i].m_pt = Point2d(ptLT * ((iTopLayer == 0) ? 1 : 2));
            vecAllResult.push_back(vecMatchParameter[i]);
        }
        else
        {
            for (int iLayer = iTopLayer - 1; iLayer >= iStopLayer; iLayer--)
            {
                // 搜索角度
                dAngleStep = angle_step;
                if (auto_angle_step)
                {
                    dAngleStep = atan(2.0 / max(pTemplateData->m_vecPyramid[iLayer].cols,
                                                pTemplateData->m_vecPyramid[iLayer].rows)) *
                                 R2D;
                }
                vector<double> vecAngles;
                double dMatchAngle = vecMatchParameter[i].m_dMatchAngle;
                // if (angle_range > 0)
                // {
                //     for (int j = -1; j <= 1; j++)
                //     {
                //         vecAngles.push_back(dMatchAngle + dAngleStep * j);
                //     }
                // }
                // else
                // {
                //     if (abs(start_angle) < VISION_TOLERANCE)
                //     {
                //         vecAngles.push_back(start_angle);  // 如果起始角度为0，则只添加0度
                //     }
                //     else 
                //     {
                //         for (int j = -1; j <= 1; j++)
                //         {
                //             vecAngles.push_back(dMatchAngle + dAngleStep * j);
                //         }
                //     }
                // }
                if (angle_range > 0)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        vecAngles.push_back(dMatchAngle + dAngleStep * j);
                    }
                }
                else
                {
                    vecAngles.push_back(dMatchAngle);
                }

                Point2f ptSrcCenter((vecMatSrcPyr[iLayer].cols - 1) / 2.0f, (vecMatSrcPyr[iLayer].rows - 1) / 2.0f);
                iSize = static_cast<int>(vecAngles.size());
                vector<MatchParameter> vecNewMatchParameter(iSize);
                int iMaxScoreIndex = 0;
                double dBigValue = -1;
                for (int j = 0; j < iSize; j++)
                {
                    Mat matResult, matRotatedSrc;
                    double dMaxValue = 0;
                    Point ptMaxLoc;
                    GetRotatedROI(vecMatSrcPyr[iLayer], pTemplateData->m_vecPyramid[iLayer].size(), ptLT * 2, vecAngles[j],
                                  matRotatedSrc);

                    MatchTemplate(matRotatedSrc, pTemplateData, matResult, iLayer, use_simd);
                    // matchTemplate (matRotatedSrc, pTemplData->vecPyramid[iLayer], matResult, CV_TM_CCOEFF_NORMED);
                    minMaxLoc(matResult, 0, &dMaxValue, 0, &ptMaxLoc);
                    vecNewMatchParameter[j] = MatchParameter(ptMaxLoc, dMaxValue, vecAngles[j]);

                    if (vecNewMatchParameter[j].m_dMatchScore > dBigValue)
                    {
                        iMaxScoreIndex = j;
                        dBigValue = vecNewMatchParameter[j].m_dMatchScore;
                    }
                    // 次像素估計
                    if (ptMaxLoc.x == 0 || ptMaxLoc.y == 0 || ptMaxLoc.x == matResult.cols - 1 ||
                        ptMaxLoc.y == matResult.rows - 1)
                        vecNewMatchParameter[j].m_bPosOnBorder = true;
                    if (!vecNewMatchParameter[j].m_bPosOnBorder)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            for (int x = -1; x <= 1; x++)
                            {
                                vecNewMatchParameter[j].m_vecResult[x + 1][y + 1] =
                                    matResult.at<float>(ptMaxLoc + Point(x, y));
                            }
                        }
                    }
                }
                if (vecNewMatchParameter[iMaxScoreIndex].m_dMatchScore < vecLayerScores[iLayer])
                    break;
                // 次像素估計
                if (sub_pixel_estimation && iLayer == 0 && (!vecNewMatchParameter[iMaxScoreIndex].m_bPosOnBorder) &&
                    iMaxScoreIndex != 0 && iMaxScoreIndex != 2)
                {
                    double dNewX = 0, dNewY = 0, dNewAngle = 0;
                    SubPixEsimation(&vecNewMatchParameter, &dNewX, &dNewY, &dNewAngle, dAngleStep, iMaxScoreIndex);
                    vecNewMatchParameter[iMaxScoreIndex].m_pt = Point2d(dNewX, dNewY);
                    vecNewMatchParameter[iMaxScoreIndex].m_dMatchAngle = dNewAngle;
                }

                double dNewMatchAngle = vecNewMatchParameter[iMaxScoreIndex].m_dMatchAngle;

                // 讓坐標系回到旋轉時(GetRotatedROI)的(0, 0)
                Point2f ptPaddingLT = ptRotatePt2f(ptLT * 2, ptSrcCenter, dNewMatchAngle * D2R) - Point2f(3, 3);
                Point2f pt(static_cast<int>(vecNewMatchParameter[iMaxScoreIndex].m_pt.x) + ptPaddingLT.x,
                           static_cast<int>(vecNewMatchParameter[iMaxScoreIndex].m_pt.y) + ptPaddingLT.y);
                // 再旋轉
                pt = ptRotatePt2f(pt, ptSrcCenter, -dNewMatchAngle * D2R);

                if (iLayer == iStopLayer)
                {
                    vecNewMatchParameter[iMaxScoreIndex].m_pt = pt * (iStopLayer == 0 ? 1 : 2);
                    vecAllResult.push_back(vecNewMatchParameter[iMaxScoreIndex]);
                }
                else
                {
                    // 更新MatchAngle ptLT
                    vecMatchParameter[i].m_dMatchAngle = dNewMatchAngle;
                    vecMatchParameter[i].m_dAngleStart = vecMatchParameter[i].m_dMatchAngle - dAngleStep / 2;
                    vecMatchParameter[i].m_dAngleEnd = vecMatchParameter[i].m_dMatchAngle + dAngleStep / 2;
                    ptLT = pt;
                }
            }
        }
    }

    // 过滤
    FilterWithScore(&vecAllResult, match_threshold);
    // 去重
    iDstW = pTemplateData->m_vecPyramid[iStopLayer].cols * (iStopLayer == 0 ? 1 : 2);
    iDstH = pTemplateData->m_vecPyramid[iStopLayer].rows * (iStopLayer == 0 ? 1 : 2);
    for (int i = 0; i < vecAllResult.size(); i++)
    {
        double dRAngle = -vecAllResult[i].m_dMatchAngle * D2R;
        Point2f ptLT = vecAllResult[i].m_pt;
        Point2f ptRT = Point2f(ptLT.x + iDstW * (float)cos(dRAngle), ptLT.y - iDstW * (float)sin(dRAngle));
        Point2f ptLB = Point2f(ptLT.x + iDstH * (float)sin(dRAngle), ptLT.y + iDstH * (float)cos(dRAngle));
        Point2f ptRB = Point2f(ptRT.x + iDstH * (float)sin(dRAngle), ptRT.y + iDstH * (float)cos(dRAngle));

        vecAllResult[i].m_rectR = RotatedRect(ptLT, ptRT, ptRB);
    }

    FilterWithRotatedRect(&vecAllResult, CV_TM_CCOEFF_NORMED, max_overlap);

    // 根据分数排序
    sort (vecAllResult.begin(), vecAllResult.end(),
         [](const MatchParameter& a, const MatchParameter& b) { return a.m_dMatchScore > b.m_dMatchScore; });
    if (vecAllResult.size() == 0)
    {
        return 0;  // 没有匹配结果
    }

    int iW = pTemplateData->m_vecPyramid[0].cols;
    int iH = pTemplateData->m_vecPyramid[0].rows;
    
    for (size_t i = 0; i < vecAllResult.size(); ++i)
    {
        const auto& result = vecAllResult[i];
        SingleTargetMatch stm;

        // 预计算三角函数值避免重复计算
        double dRAngle = -result.m_dMatchAngle * D2R;
        double cosA = std::cos(dRAngle);
        double sinA = std::sin(dRAngle);

        // 计算四个角点坐标
        Point2d ptLT = result.m_pt;
        Point2d ptRT(ptLT.x + iW * cosA, ptLT.y - iW * sinA);
        Point2d ptLB(ptLT.x + iH * sinA, ptLT.y + iH * cosA);
        Point2d ptRB(ptRT.x + iH * sinA, ptRT.y + iH * cosA);

        // 按顺序存储四个角点的坐标
        std::array<Point2d, 4> points = {ptLT, ptRT, ptLB, ptRB};
        for (size_t j = 0; j < 4; ++j)
        {
            stm.m_dPoints[j * 2] = points[j].x;
            stm.m_dPoints[j * 2 + 1] = points[j].y;
        }

        // 设置角度并确保在[-180, 180]范围内
        stm.m_dAngle = -result.m_dMatchAngle;
        if (stm.m_dAngle < -180)
            stm.m_dAngle += 360;
        else if (stm.m_dAngle > 180)
            stm.m_dAngle -= 360;

        stm.m_dScore = result.m_dMatchScore;
        stm.m_iIndex = static_cast<int>(i);

        out_array[i] = stm;
    }

    return vecAllResult.size() > max_match_count ? max_match_count
                                                 : static_cast<int>(vecAllResult.size());  // 这里需要实现具体的匹配逻辑
}