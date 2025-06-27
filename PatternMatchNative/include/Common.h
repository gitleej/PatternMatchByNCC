#pragma once

#ifndef _COMMON_H_
    #define _COMMON_H_

    #include <opencv2/core/core.hpp>
    #include <vector>


using namespace cv;
using namespace std;

namespace PatternMatch
{
    /// <summary>
    /// 模板数据类
    /// </summary>
    class TemplateData
    {
    public:
        TemplateData();
        ~TemplateData();

    public:
        std::vector<Mat> m_vecPyramid;
        vector<Scalar> m_vecTemplMean;
        vector<double> m_vecTemplNorm;
        vector<double> m_vecInvArea;
        vector<bool> m_vecResultEqual1;
        bool m_bIsPatternLearned;
        int m_iBorderColor;
        int m_iPyramidLayers;

    public:
        /// <summary>
        /// 清空模板数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 重设模板数据大小
        /// </summary>
        void Resize();
    };
}  // namespace PatternMatch

#endif  // _COMMON_H_
