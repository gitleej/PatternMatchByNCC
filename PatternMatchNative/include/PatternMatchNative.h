#pragma once

#ifndef _PATTERN_MATCH_NATIVE_H_
    #define _PATTERN_MATCH_NATIVE_H_

    #ifdef _WIN32
        #ifdef PATTERN_MATCH_NATIVE_EXPORTS
            #define PM_API __declspec(dllexport)
        #else
            #define PM_API __declspec(dllimport)
        #endif  // PATTERN_MATCH_NATIVE_EXPORTS is defined when building the
                // library
    #else
        #define PM_API
    #endif
#include "Common.h"

extern "C" {
/**
 * @brief 创建一个PatternMatchNative类的实例
 * @return 返回类指针
 */
PM_API void* PM_Create();

/**
 * @brief 销毁PatternMatchNative类的实例
 * @param instance 待销毁实例指针
 */
PM_API void PM_Destroy(void* instance);

/**
 * 学习模板图像
 * @param instance 实例指针
 * @param pTemplImageData 模板图像数据指针
 * @param iTemplWidth 模板图像宽度
 * @param iTemplHeight 模板图像高度
 * @param iTemplStride 模板图像步长
 * @param iPyramidLayers 图像金字塔层数
 * @param iMinReduceSize 图像最小缩小尺寸
 * @param bAutoPyramidLayers 是否自动计算金字塔层数
 */
PM_API void Learn_Pattern(void* instance,
                          const unsigned char* pTemplImageData,
                          int iTemplWidth,
                          int iTemplHeight,
                          int iTemplStride,
                          int iPyramidLayers,
                          int iMinReduceSize,
                          bool bAutoPyramidLayers);

/**
 * 可视化模板图像金字塔
 * @param instance 实例指针
 */
PM_API int  NV_Get_Template_Pyramid_Layers(void* instance);

/**
 * 获取图像金字塔信息
 * @param instance 实例指针
 * @param index 图像金字塔索引
 * @param outRows 图像宽度
 * @param outCols 图像高度
 * @param outType 图像类型
 * @param outStep 图像步长
 * @return 返回图像金字塔数据指针
 */
PM_API unsigned char* NV_Get_Template_Pyramid(
    void* instance, int index, int* outRows, int* outCols, int* outType, int* outStep);

/**
 * @brief 模板匹配导出函数
 * @param instance 实例指针
 * @param srcImageData 源图像数据指针
 * @param srcImageWidth 源图像宽度
 * @param srcImageHeight 源图像高度
 * @param srcImageStride 源图像步长
 * @param outArray 输出匹配结果
 * @param srcReverse 是否反转源图像
 * @param angleStep 旋转角度步长
 * @param autoAngleStep 是否启用自动计算旋转角度步长
 * @param startAngle 匹配起始角度
 * @param angleRange 匹配角度范围
 * @param matchThreshold 匹配阈值
 * @param maxMatchCount 最大匹配目标数量
 * @param useSIMD 是否使用SIMD加速
 * @param maxOverlap 匹配目标最大重叠率
 * @param subPixelEstimation 是否启用亚像素估计
 * @param fastMode 是否启用快速匹配模式
 * @param debug 是否启用调试模式
 * @return 实际匹配目标数量
 */
PM_API int NV_Match(void* instance,                     // 实例指针
                    const unsigned char* srcImageData,  // 源图像数据指针
                    int srcImageWidth,                  // 源图像宽度
                    int srcImageHeight,                 // 源图像高度
                    int srcImageStride,                 // 源图像步长
                    PatternMatch::SingleTargetMatch* outArray,
                    bool srcReverse = false,         // 是否反转源图像
                    double angleStep = 10,           // 旋转角度步长
                    bool autoAngleStep = true,       // 是否自动计算角度步长
                    double startAngle = 0,           // 起始匹配角度
                    double angleRange = 360,         // 匹配角度范围
                    double matchThreshold = 0.9,     // 匹配阈值
                    int maxMatchCount = 70,          // 最大匹配数量
                    bool useSIMD = true,             // 是否使用SIMD优化
                    double maxOverlap = 0,           // 最大重叠比例
                    bool subPixelEstimation = true,  // 是否进行亚像素估计
                    bool fastMode = false,           // 是否使用快速模式
                    bool debug = false               // 是否启用调试模式
);
}

#endif  // _PATTERN_MATCH_NATIVE_H_
