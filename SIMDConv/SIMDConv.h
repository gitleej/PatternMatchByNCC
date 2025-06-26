#pragma once

using namespace System;

namespace SIMDConv {

	public ref class SIMDWrapper
	{
	public:
		static int IM_Conv_SIMD(IntPtr pKernel, IntPtr pConv, int length);
		static void MatchTemplate_SIMD(
			const unsigned char* pSrc, int srcWidth, int srcHeight, int srcStride,
			const unsigned char* pTpl, int tplWidth, int tplHeight, int tplStride,
			const unsigned char* pResult, int resultWidth, int resultHeight, int resultStride // stride in float units
		);
	};
}
