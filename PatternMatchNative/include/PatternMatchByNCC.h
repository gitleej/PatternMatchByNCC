#pragma once
#ifndef _PATTERN_MATCH_BY_NCC_H_
#define _PATTERN_MATCH_BY_NCC_H_

#include <opencv2/core/core.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/highgui/highgui.hpp>
#include <Common.h>

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
		
		unsigned char *GetPyramidInfo(int index, int* out_rows, int* out_cols, int* out_type, int* out_step);
	};
}

#endif // _PATTERN_MATCH_BY_NCC_H_

