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

}

#endif  // _PATTERN_MATCH_NATIVE_H_
