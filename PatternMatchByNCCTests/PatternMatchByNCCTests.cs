using Microsoft.VisualStudio.TestTools.UnitTesting;
using PatternMatchByNCC;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace PatternMatchByNCC.Tests
{
    [TestClass()]
    public class PatternMatchByNCCTests
    {
        private PatternMatch.PatternMatchByNCC _matcher;

        [TestInitialize]
        public void Setup()
        {
            _matcher = new PatternMatch.PatternMatchByNCC();
        }

        [TestMethod]
        [Description("测试纯色图像的处理")]
        public void LearnPattern_SolidColorImage_Success()
        {
            // Arrange
            using (var matTemp = Mat.Ones(64, 64, MatType.CV_8UC1) * 255)  // 纯白图像
            {
                // Act
                _matcher.LearnPattern(matTemp, 3, 16, true);

                // Assert
                Assert.IsTrue(_matcher.TemplateData.IsPatternLearned);
                Assert.AreEqual(3, _matcher.TemplateData.VecPyramid.Count);
                Assert.AreEqual(255, _matcher.TemplateData.BorderColor);
                Assert.IsTrue(_matcher.TemplateData.VecResultEqual1[0]); // 应该检测为纯色图像
            }
        }

        [TestMethod]
        [Description("测试带有渐变的图像")]
        public void LearnPattern_GradientImage_Success()
        {
            // Arrange
            using (var matTemp = new Mat(64, 64, MatType.CV_8UC1))
            {
                // 创建渐变图像
                for (int i = 0; i < matTemp.Rows; i++)
                {
                    for (int j = 0; j < matTemp.Cols; j++)
                    {
                        matTemp.Set(i, j, (i + j) * 2);
                    }
                }

                // Act
                _matcher.LearnPattern(matTemp, 3, 16, true);

                // Assert
                Assert.IsTrue(_matcher.TemplateData.IsPatternLearned);
                Assert.AreEqual(3, _matcher.TemplateData.VecPyramid.Count);
                Assert.IsFalse(_matcher.TemplateData.VecResultEqual1[0]); // 不应该检测为纯色图像
            }
        }

        [TestMethod]
        [Description("测试自动计算金字塔层数")]
        public void LearnPattern_AutoPyramidLayers_Success()
        {
            // Arrange
            using (var matTemp = new Mat(256, 256, MatType.CV_8UC1))
            {
                matTemp.SetTo(128);

                // Act
                _matcher.LearnPattern(matTemp, 5, 16, true);

                // Assert
                Assert.IsTrue(_matcher.TemplateData.IsPatternLearned);
                // 256x256 -> 128x128 -> 64x64 -> 32x32 -> 16x16 -> 8x8 (5层)
                Assert.AreEqual(5, _matcher.TemplateData.VecPyramid.Count);
            }
        }

        [TestMethod]
        [Description("测试输入验证")]
        public void LearnPattern_InvalidInput_ThrowsException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                _matcher.LearnPattern(null, 3, 16, true));

            using (var matTemp = new Mat(1, 1, MatType.CV_8UC1))
            {
                Assert.ThrowsException<ArgumentException>(() =>
                    _matcher.LearnPattern(matTemp, -1, 16, true));

                Assert.ThrowsException<ArgumentException>(() =>
                    _matcher.LearnPattern(matTemp, 3, 0, false));
            }
        }

        [TestMethod]
        [Description("测试统计参数计算")]
        public void LearnPattern_StatisticalParameters_Success()
        {
            // Arrange
            using (var matTemp = Mat.Ones(64, 64, MatType.CV_8UC1) * 128)  // 灰度值128的图像
            {
                // Act
                _matcher.LearnPattern(matTemp, 2, 16, true);

                // Assert
                Assert.IsTrue(_matcher.TemplateData.IsPatternLearned);
                Assert.AreEqual(2, _matcher.TemplateData.VecPyramid.Count);

                // 验证逆面积计算
                double expectedInvArea = 1.0 / (64 * 64);
                Assert.AreEqual(expectedInvArea, _matcher.TemplateData.VecInvArea[0], 1e-10);

                // 验证均值
                Assert.AreEqual(128, _matcher.TemplateData.VecTemplMean[0].Val0, 1);
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            Cv2.DestroyAllWindows();
        }
    }
}