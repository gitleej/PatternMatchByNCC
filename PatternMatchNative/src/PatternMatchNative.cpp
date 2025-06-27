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