#include "pch.h"

#include "SIMDConv.h"

#include <emmintrin.h> // SSE2
#include <iostream>
#include <ostream>

using namespace SIMDConv;

// 提前声明 Native 函数（注意不是托管函数）
int Native_IM_Conv_SIMD(const unsigned char* pCharKernel, const unsigned char* pCharConv, int iLength);

int SIMDWrapper::IM_Conv_SIMD(IntPtr pKernel, IntPtr pConv, int length)
{
	if (length <= 0 || pKernel == IntPtr::Zero || pConv == IntPtr::Zero)
		return 0;

	unsigned char* pk = static_cast<unsigned char*>(pKernel.ToPointer());
	unsigned char* pc = static_cast<unsigned char*>(pConv.ToPointer());

	return Native_IM_Conv_SIMD(pk, pc, length);
}

void SIMDWrapper::MatchTemplate_SIMD(
	const unsigned char* pSrc, int srcWidth, int srcHeight, int srcStride,
	const unsigned char* pTpl, int tplWidth, int tplHeight, int tplStride,
	const unsigned char* pResult, int resultWidth, int resultHeight, int resultStride // stride in float units
)
{
	float* result = (float*)pResult;

	for (int r = 0; r < resultHeight; ++r)
	{
		float* r_matResult = result + r * resultStride;
		const unsigned char* r_source = pSrc + r * srcStride;

		for (int c = 0; c < resultWidth; ++c, ++r_matResult, ++r_source)
		{
			float sum = 0.0f;
			const unsigned char* r_template = pTpl;
			const unsigned char* r_sub_source = r_source;

			for (int t_r = 0; t_r < tplHeight; ++t_r, r_sub_source += srcStride, r_template += tplStride)
			{
				sum += (float)Native_IM_Conv_SIMD(r_template, r_sub_source, tplWidth);
			}
			*r_matResult = sum;
			//std::cout << sum << std::endl;
		}
		//std::cout << std::endl;
	}

	// 格式化输出pResult内容为矩阵
	/*std::cout << "matResult (" << resultHeight << "x" << resultWidth << "):" << std::endl;
	for (int r = 0; r < resultHeight; ++r)
	{
		float* r_matResult = result + r * resultStride;
		for (int c = 0; c < resultWidth; ++c)
		{
			std::cout.width(10);
			std::cout.precision(4);
			std::cout << std::fixed << r_matResult[c] << " ";
		}
		std::cout << std::endl;
	}
	std::cout << std::endl;*/
}

#pragma managed(push, off)
int Native_IM_Conv_SIMD(const unsigned char* pCharKernel, const unsigned char* pCharConv, int iLength)
{
	const int iBlockSize = 16;
	int Block = iLength / iBlockSize;
	__m128i SumV = _mm_setzero_si128();
	__m128i Zero = _mm_setzero_si128();

	for (int Y = 0; Y < Block * iBlockSize; Y += iBlockSize)
	{
		__m128i SrcK = _mm_loadu_si128((__m128i*)(pCharKernel + Y));
		__m128i SrcC = _mm_loadu_si128((__m128i*)(pCharConv + Y));
		__m128i SrcK_L = _mm_unpacklo_epi8(SrcK, Zero);
		__m128i SrcK_H = _mm_unpackhi_epi8(SrcK, Zero);
		__m128i SrcC_L = _mm_unpacklo_epi8(SrcC, Zero);
		__m128i SrcC_H = _mm_unpackhi_epi8(SrcC, Zero);
		__m128i SumT = _mm_add_epi32(_mm_madd_epi16(SrcK_L, SrcC_L), _mm_madd_epi16(SrcK_H, SrcC_H));
		SumV = _mm_add_epi32(SumV, SumT);
	}

	// Horizontal sum
	int temp[4];
	_mm_storeu_si128((__m128i*)temp, SumV);
	int sum = temp[0] + temp[1] + temp[2] + temp[3];

	for (int Y = Block * iBlockSize; Y < iLength; Y++)
	{
		sum += pCharKernel[Y] * pCharConv[Y];
	}

	return sum;
}
#pragma managed(pop)
