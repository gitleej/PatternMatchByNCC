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

    /**
     * @brief 单个目标匹配结果
     */
    struct SingleTargetMatch
    {
        /**
         * @brief 匹配框的四个顶点坐标
         */
        double m_dPoints[8];
        /**
         * @brief 匹配得分
         */
        double m_dScore;
        /**
         * @brief 匹配角度
         */
        double m_dAngle;
        /**
         * @brief 目标索引值
         */
        int m_iIndex;
    };

    class MatchParameter
    {
    public:
        /**
         * @brief 带参数的构造函数
         * @param ptMinMax 匹配点的最小最大值
         * @param dScore 匹配得分
         * @param dAngle 匹配角度
         */
        MatchParameter(Point2f ptMinMax, double dScore, double dAngle);

        /**
         * @brief 无参构造函数
         */
        MatchParameter();

        /**
         * @brief 析构函数
         */
        ~MatchParameter() = default;

    public:
        Point2d m_pt;
        Point2d m_ptSubPixel;
        double m_dMatchScore;
        double m_dMatchAngle;
        double m_dAngleStart;
        double m_dAngleEnd;
        double m_vecResult[3][3];  // for subpixel
        double m_dNewAngle;
        // Mat matRotatedSrc;
        Rect m_rectRoi;
        Rect m_rectBounding;
        
        RotatedRect m_rectR;
        
        int m_iMaxScoreIndex;  // for subpixel

        bool m_bDelete;
        bool m_bPosOnBorder;
    };

    class Block
    {
    public:
        Rect m_rect;
        double m_dMax;
        Point m_ptMaxLoc;

    public:
        Block();
        Block(Rect rect, double dMax, Point ptMaxLoc);
        ~Block();
    };

    class BlockMax
    {
    public:
        BlockMax();
        ~BlockMax();

        BlockMax(Mat matSrc, Size sizeTemplate);

        void UpdateMax(Rect rectIgnore);

        void GetMaxValueLoc(double &dMax, Point &ptMaxLoc);

    public:
        vector<Block> m_vecBlock;  // 匹配块列表
        Mat m_matSrc;              // 源图像
    };
}  // namespace PatternMatch

#endif  // _COMMON_H_
