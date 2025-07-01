#include "Common.h"

PatternMatch::TemplateData::TemplateData():m_iBorderColor(0), m_iPyramidLayers(0) 
{
    m_bIsPatternLearned = false;
}

PatternMatch::TemplateData::~TemplateData() = default;

void PatternMatch::TemplateData::Clear()
{
    vector<Mat>().swap(m_vecPyramid);
    vector<double>().swap(m_vecTemplNorm);
    vector<double>().swap(m_vecInvArea);
    vector<Scalar>().swap(m_vecTemplMean);
    vector<bool>().swap(m_vecResultEqual1);
}

void PatternMatch::TemplateData::Resize()
{
    int iSize = static_cast<int>(m_vecPyramid.size());
    m_vecTemplMean.resize(iSize);
    m_vecTemplNorm.resize(iSize, 0);
    m_vecInvArea.resize(iSize, 1);
    m_vecResultEqual1.resize(iSize, false);
    m_iPyramidLayers = iSize;
}

 PatternMatch::MatchParameter::MatchParameter(Point2f ptMinMax, double dScore, double dAngle)
    : m_dAngleStart(0), m_dAngleEnd(0), m_vecResult{{0, 0, 0}, {0, 0, 0}, {0, 0, 0}}, m_iMaxScoreIndex(0)
{
    m_pt = ptMinMax;
    m_dMatchScore = dScore;
    m_dMatchAngle = dAngle;

    m_bDelete = false;
    m_dNewAngle = 0.0;

    m_bPosOnBorder = false;
}

 PatternMatch::MatchParameter::MatchParameter()
    : m_dAngleStart(0), m_dAngleEnd(0), m_vecResult{{0, 0, 0}, {0, 0, 0}, {0, 0, 0}}, m_iMaxScoreIndex(0)
{
    m_pt = Point2d();
    m_dMatchScore = 0.0;
    m_dMatchAngle = 0.0;

    m_bDelete = false;
    m_dNewAngle = 0.0;

    m_bPosOnBorder = false;
}

PatternMatch::Block::Block() = default;

PatternMatch::Block::Block(Rect rect, double dMax, Point ptMaxLoc)
{
    m_rect = rect;
    m_dMax = dMax;
    m_ptMaxLoc = ptMaxLoc;
}

PatternMatch::Block::~Block() = default;

PatternMatch::BlockMax::BlockMax() = default;

PatternMatch::BlockMax::~BlockMax() = default;

PatternMatch::BlockMax::BlockMax(Mat matSrc, Size sizeTemplate)
{
    m_matSrc = matSrc;
    // 將matSrc 拆成數個block，分別計算最大值
    int iBlockW = sizeTemplate.width * 2;
    int iBlockH = sizeTemplate.height * 2;

    int iCol = matSrc.cols / iBlockW;
    bool bHResidue = matSrc.cols % iBlockW != 0;

    int iRow = matSrc.rows / iBlockH;
    bool bVResidue = matSrc.rows % iBlockH != 0;

    if (iCol == 0 || iRow == 0)
    {
        m_vecBlock.clear();
        return;
    }

    m_vecBlock.resize(iCol * iRow);
    int iCount = 0;
    for (int y = 0; y < iRow; y++)
    {
        for (int x = 0; x < iCol; x++)
        {
            Rect rectBlock(x * iBlockW, y * iBlockH, iBlockW, iBlockH);
            m_vecBlock[iCount].m_rect = rectBlock;
            minMaxLoc(matSrc(rectBlock), 0, &m_vecBlock[iCount].m_dMax, 0, &m_vecBlock[iCount].m_ptMaxLoc);
            m_vecBlock[iCount].m_ptMaxLoc += rectBlock.tl();
            iCount++;
        }
    }
    if (bHResidue && bVResidue)
    {
        Rect rectRight(iCol * iBlockW, 0, matSrc.cols - iCol * iBlockW, matSrc.rows);
        Block blockRight;
        blockRight.m_rect = rectRight;
        minMaxLoc(matSrc(rectRight), 0, &blockRight.m_dMax, 0, &blockRight.m_ptMaxLoc);
        blockRight.m_ptMaxLoc += rectRight.tl();
        m_vecBlock.push_back(blockRight);

        Rect rectBottom(0, iRow * iBlockH, iCol * iBlockW, matSrc.rows - iRow * iBlockH);
        Block blockBottom;
        blockBottom.m_rect = rectBottom;
        minMaxLoc(matSrc(rectBottom), 0, &blockBottom.m_dMax, 0, &blockBottom.m_ptMaxLoc);
        blockBottom.m_ptMaxLoc += rectBottom.tl();
        m_vecBlock.push_back(blockBottom);
    }
    else if (bHResidue)
    {
        Rect rectRight(iCol * iBlockW, 0, matSrc.cols - iCol * iBlockW, matSrc.rows);
        Block blockRight;
        blockRight.m_rect = rectRight;
        minMaxLoc(matSrc(rectRight), 0, &blockRight.m_dMax, 0, &blockRight.m_ptMaxLoc);
        blockRight.m_ptMaxLoc += rectRight.tl();
        m_vecBlock.push_back(blockRight);
    }
    else
    {
        Rect rectBottom(0, iRow * iBlockH, matSrc.cols, matSrc.rows - iRow * iBlockH);
        Block blockBottom;
        blockBottom.m_rect = rectBottom;
        minMaxLoc(matSrc(rectBottom), 0, &blockBottom.m_dMax, 0, &blockBottom.m_ptMaxLoc);
        blockBottom.m_ptMaxLoc += rectBottom.tl();
        m_vecBlock.push_back(blockBottom);
    }
}

void PatternMatch::BlockMax::UpdateMax(Rect rectIgnore)
{
    if (static_cast<int>(m_vecBlock.size()) == 0)
        return;
    // 找出所有跟rectIgnore交集的block
    int iSize = static_cast<int>(m_vecBlock.size());
    for (int i = 0; i < iSize; i++)
    {
        Rect rectIntersec = rectIgnore & m_vecBlock[i].m_rect;
        // 無交集
        if (rectIntersec.width == 0 && rectIntersec.height == 0)
            continue;
        // 有交集，更新極值和極值位置
        minMaxLoc(m_matSrc(m_vecBlock[i].m_rect), 0, &m_vecBlock[i].m_dMax, 0, &m_vecBlock[i].m_ptMaxLoc);
        m_vecBlock[i].m_ptMaxLoc += m_vecBlock[i].m_rect.tl();
    }
}

void PatternMatch::BlockMax::GetMaxValueLoc(double& dMax, Point& ptMaxLoc)
{
    int iSize = static_cast<int>(m_vecBlock.size());
    if (iSize == 0)
    {
        minMaxLoc(m_matSrc, 0, &dMax, 0, &ptMaxLoc);
        return;
    }
    // 從block中找最大值
    int iIndex = 0;
    dMax = m_vecBlock[0].m_dMax;
    for (int i = 1; i < iSize; i++)
    {
        if (m_vecBlock[i].m_dMax >= dMax)
        {
            iIndex = i;
            dMax = m_vecBlock[i].m_dMax;
        }
    }
    ptMaxLoc = m_vecBlock[iIndex].m_ptMaxLoc;
}

