#include "PatternMatchNative.h"

#include "PatternMatchByNCC.h"

/**
 * @brief 创建一个PatternMatchNative类的实例
 * @return 返回实例指针
 */
void* PM_Create()
{
	return new PatternMatch::PatternMatchByNCC();
}

/**
 * @brief 销毁PatternMatchNative类的实例
 * @param instance 待销毁实例指针
 */
void PM_Destroy(void* instance)
{
	delete static_cast<PatternMatch::PatternMatchByNCC*>(instance);
}

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
void Learn_Pattern(void* instance,
                   const unsigned char* pTemplImageData,
                   int iTemplWidth,
                   int iTemplHeight,
                   int iTemplStride,
                   int iPyramidLayers,
                   int iMinReduceSize,
                   bool bAutoPyramidLayers) {
    static_cast<PatternMatch::PatternMatchByNCC*>(instance)->Learn_Pattern(
        pTemplImageData, iTemplWidth, iTemplHeight, iTemplStride,
        iPyramidLayers, iMinReduceSize, bAutoPyramidLayers);
}

/**
 * 可视化模板图像金字塔
 * @param instance 实例指针
 */
int  NV_Get_Template_Pyramid_Layers(void* instance)
{
    return static_cast<PatternMatch::PatternMatchByNCC*>(instance)->GetTemplatePyramidLayers();
}

unsigned char* NV_Get_Template_Pyramid(
    void* instance, int index, int* outRows, int* outCols, int* outType, int* outStep)
{
    return static_cast<PatternMatch::PatternMatchByNCC*>(instance)->GetPyramidInfo(index, outRows, outCols, outType,
                                                                                   outStep);
}

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
int NV_Match(void* instance,
             const unsigned char* srcImageData,  // 源图像数据指针
             int srcImageWidth,                  // 源图像宽度
             int srcImageHeight,                 // 源图像高度
             int srcImageStride,                 // 源图像步长
             PatternMatch::SingleTargetMatch* outArray,
             bool srcReverse,
             double angleStep,
             bool autoAngleStep,
             double startAngle,
             double angleRange,
             double matchThreshold,
             int maxMatchCount,
             bool useSIMD,
             double maxOverlap,
             bool subPixelEstimation,
             bool fastMode,
             bool debug)
{
    return static_cast<PatternMatch::PatternMatchByNCC*>(instance)->Match(
        srcImageData, srcImageWidth, srcImageHeight, srcImageStride,
        outArray, srcReverse, angleStep, autoAngleStep, startAngle, angleRange,
        matchThreshold, maxMatchCount, useSIMD, maxOverlap, subPixelEstimation, fastMode, debug);
}