#pragma once
#ifndef _PATTERN_MATCH_BY_NCC_H_
#define _PATTERN_MATCH_BY_NCC_H_

#include <opencv2/core/core.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/imgproc/imgproc_c.h>
#include <Common.h>

#include <iostream>
#include <string>

#define R2D 180.0 / CV_PI
#define D2R CV_PI / 180.0
#define VISION_TOLERANCE 0.0000001
#define MATCH_CANDIDATE_NUM 5

using namespace cv;

namespace PatternMatch
{
	class PatternMatchByNCC
	{
    private:
        Mat m_matTemplImage;  // 模板图像
        TemplateData m_templateData;  // 模板数据

	public:
        PatternMatchByNCC();

		int GetTopLayer(Mat* matTempl, int minTemplLength);

		/**
		 * @brief 学习模板图像
		 * @param pTemplImageData 模板图像数据指针
		 * @param iTemplWidth 模板图像宽度
		 * @param iTemplHeight 模板图像高度
		 * @param iTemplStride 模板图像行步长
		 * @param iPyramidLayers 金字塔层数
		 * @param iMinReduceSize 最小缩放尺寸
		 * @param bAutoPyramidLayers 是否自动计算金字塔层数
         */
		void Learn_Pattern(const unsigned char* pTemplImageData,
			int iTemplWidth,
			int iTemplHeight,
			int iTemplStride,
			int iPyramidLayers,
			int iMinReduceSize,
			bool bAutoPyramidLayers);

		/// <summary>
        /// 获取模板图像金字塔的层数
		/// </summary>
		/// <returns></returns>
		int GetTemplatePyramidLayers();
		
		unsigned char* GetPyramidInfo(int index, int* out_rows, int* out_cols, int* out_type, int* out_step);

        /**
         * @brief 计算点绕中心点旋转后的新坐标
         * @param ptInput 输入点
         * @param ptOrg 旋转中心
         * @param dAngle 旋转角度
         * @return 旋转后的点
         */
        Point2f ptRotatePt2f(Point2f ptInput, Point2f ptOrg, double dAngle);

        /**
         * @brief 获取最佳旋转图像尺寸
         * @param sizeSrc 源图像尺寸
         * @param sizeDst 模板图像尺寸
         * @param dRAngle 旋转角度
         * @return 最佳旋转图像尺寸
         */
        Size GetBestRotationSize(Size sizeSrc, Size sizeDst, double dRAngle);

        /**
         * @brief 使用SSE指令集加速的模板匹配卷积运算
         * @param pCharKernel 模板图像数据
         * @param pCharConv 待匹配图像数据
         * @param iLength 数据长度
         * @return 卷积结果
         */
        int IM_Conv_SIMD(const unsigned char* pCharKernel, const unsigned char* pCharConv, int iLength);

        /**
         * @brief 计算CCOEFF相关系数的分母
         * @param matSrc 源图像Mat对象
         * @param pTemplateData 模板数据指针
         * @param matResult 匹配结果Mat对象
         * @param iLayer 金字塔层级
         */
        void CCOEFF_Denominator(const Mat& matSrc, TemplateData* pTemplateData, Mat& matResult, int iLayer);

        /**
         * @brief 模板匹配
         * @param matSrc 源图像Mat对象
         * @param pTemplateData 模板数据指针
         * @param matResult 匹配结果Mat对象
         * @param iLayer 金字塔层级
         * @param bUseSIMD 是否使用SIMD加速
         */
        void MatchTemplate(const Mat& matSrc,
                           TemplateData* pTemplateData,
                           Mat& matResult,
                           int iLayer,
                           bool bUseSIMD);
        
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
        Point GetNextMaxLoc(const Mat& matResult,
                            Point ptMaxLoc,
                            Size sizeTemplate,
                            double& dMaxValue,
                            double dMaxOverlap,
                            BlockMax& blockMax);

        /**
         * @brief 获取下一个最大位置
         * @param matResult 匹配结果矩阵
         * @param ptMaxLoc 当前最大位置
         * @param sizeTemplate 模板尺寸
         * @param dMaxValue 最大值输出参数
         * @param dMaxOverlap 最大重叠率
         * @return 下一个最大位置
         */
        Point GetNextMaxLoc(const Mat& matResult,
                            Point ptMaxLoc,
                            Size sizeTemplate,
                            double& dMaxValue,
                            double dMaxOverlap);
        
        /**
         * @brief 获取旋转后的ROI区域
         * @param matSrc 源图像Mat对象
         * @param size 目标尺寸
         * @param ptLT 左上角点
         * @param dAngle 旋转角度
         * @param matRoi 输出的ROI区域Mat对象
         */
        void GetRotatedROI(Mat& matSrc, Size size, Point2f ptLT, double dAngle, Mat& matRoi);
        
        /**
         * @brief 亚像素级估计
         * @param vec 匹配结果向量
         * @param dNewX 输出的新X坐标
         * @param dNewY 输出的新Y坐标
         * @param dNewAngle 输出的新角度
         * @param dAngleStep 角度步长
         * @param iMaxScoreIndex 最大得分索引
         * @return 是否成功估计
         */
        bool SubPixEsimation(vector<MatchParameter>* vec,
                             double* dNewX,
                             double* dNewY,
                             double* dNewAngle,
                             double dAngleStep,
                             int iMaxScoreIndex);

        /**
         * @brief 过滤匹配结果，根据得分阈值
         * @param vec 匹配结果
         * @param dScore 得分阈值
         */
        void FilterWithScore(vector<MatchParameter>* vec, double dScore);

        /**
         * @brief 对点进行排序，按中心点顺时针方向
         * @param vecSort 待排序的点向量
         */
        void SortPtWithCenter(vector<Point_<float>>& vecSort);
        /**
         * @brief 过滤匹配结果，根据旋转矩形
         * @param vec 匹配结果向量
         * @param iMethod 匹配方法
         * @param dMaxOverLap 最大重叠率
         */
        void FilterWithRotatedRect(vector<MatchParameter>* vec, int iMethod, double dMaxOverLap);

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
        int Match(const unsigned char* srcImageData,  // 源图像数据指针
                  int srcImageWidth,                  // 源图像宽度
                  int srcImageHeight,                 // 源图像高度
                  int srcImageStride,                 // 源图像步长
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
                  bool debug);
	};
}

#endif // _PATTERN_MATCH_BY_NCC_H_

