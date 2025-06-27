#include "Common.h"

PatternMatch::TemplateData::TemplateData()
{
    m_bIsPatternLearned = false;
}

PatternMatch::TemplateData::~TemplateData() {}

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
