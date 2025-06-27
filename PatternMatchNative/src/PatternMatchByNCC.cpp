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
    *out_step = pyramidLayer.step;  // 获取行步长

    return pyramidLayer.data;  // 返回数据指针
}