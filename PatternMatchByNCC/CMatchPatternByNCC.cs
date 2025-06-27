using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace PatternMatch
{
    /// <summary>
    /// 非托管类，用于模式匹配的归一化互相关（NCC）算法。
    /// </summary>
    public class CMatchPatternByNCC : IDisposable
    {
        private IntPtr _native;

        [DllImport("PatternMatchNative.dll", EntryPoint = "PM_Create", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr CPMCreate();

        [DllImport("PatternMatchNative.dll", EntryPoint = "PM_Destroy", CallingConvention = CallingConvention.Cdecl)]
        static extern void CPMDestroy(IntPtr native);

        [DllImport("PatternMatchNative.dll", EntryPoint = "Learn_Pattern", CallingConvention = CallingConvention.Cdecl)]
        static extern void CLearnPattern(IntPtr native,
            IntPtr pTemplImageData,
            int iTemplWidth,
            int iTemplHeight,
            int iTemplStride,
            int iPyramidLayers,
            int iMinReduceSize,
            [MarshalAs(UnmanagedType.I1)] 
            bool bAutoPyramidLayers);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Get_Template_Pyramid_Layers", CallingConvention = CallingConvention.Cdecl)]
        static extern int CGetTemplatePyramidLayers(IntPtr native);

        [DllImport("PatternMatchNative.dll", EntryPoint = "NV_Get_Template_Pyramid", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr CGetTemplatePyramid(IntPtr native, int index, out int rows, out int cols, out int type, out int step);



        public CMatchPatternByNCC()
        {
            _native = CPMCreate();
            if (_native == IntPtr.Zero)
            {
                throw new Exception("Failed to create native CMatchPatternByNCC instance.");
            }
        }

        public void Dispose()
        {
            if (_native != IntPtr.Zero)
            {
                CPMDestroy(_native);
                _native = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 学习模板图像金字塔
        /// </summary>
        /// <param name="templateImage">输入模板图像</param>
        /// <param name="pyramidLayers">图像金字塔层数</param>
        /// <param name="minReduceSize">最小图像缩放尺寸</param>
        /// <param name="autoPyramidLayers">是否自动计算图像金字塔层数</param>
        public void LearnPattern(Mat templateImage, int pyramidLayers, int minReduceSize, bool autoPyramidLayers)
        {
            unsafe
            {
                IntPtr pTemplateImageData =  (IntPtr)templateImage.Data;
                int templateWidth = templateImage.Width;
                int templateHeight = templateImage.Height;
                int templateStride = (int)templateImage.Step();
                if (_native == IntPtr.Zero)
                {
                    throw new ObjectDisposedException("CMatchPatternByNCC");
                }

                CLearnPattern(_native, pTemplateImageData, templateWidth, templateHeight, templateStride,
                    pyramidLayers, minReduceSize, autoPyramidLayers);
            }
        }

        /// <summary>
        /// 显示模板图像金字塔
        /// </summary>
        public void ShowTemplatePyramid()
        {
            int templatePyramidLayers = CGetTemplatePyramidLayers(_native);
            for (int i = 0; i < templatePyramidLayers; i++)
            {
                IntPtr ptr = CGetTemplatePyramid(_native, i, out int rows, out int cols, out int type, out int step);
                if (ptr == IntPtr.Zero)
                {
                    continue;
                }

                // Updated to use Mat.FromPixelData instead of the obsolete constructor
                Mat pyramidImage = Mat.FromPixelData(rows, cols, (MatType)type, ptr, (int)step);
                Cv2.ImShow($"pyramid:{i}", pyramidImage);
                Cv2.WaitKey(0);
            }

            Cv2.DestroyAllWindows();
        }
    }
}
