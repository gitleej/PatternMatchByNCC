# PatternMatchByNCC

本项目是一个基于归一化互相关（NCC, Normalized Cross Correlation）算法的高效图像模板匹配库，包含C++原生实现和C# .NET Framework 4.8的托管调用示例。项目适用于工业视觉、目标检测、模板定位等场景，支持多层图像金字塔、旋转、亚像素精度估计和SIMD加速。

# 目录结构

```
PatternMatchByNCC/ 
├── PatternMatchByNCCConsoleApp/         # C# 控制台测试程序（入口Main函数） 
│   ├── Program.cs                       # 主程序，演示如何调用匹配接口 
│   └── ...                              # 其它C#辅助文件 
│ ├── PatternMatchByNCC/                 # C# 封装库，P/Invoke调用C++ DLL 
│   ├── CMatchPatternByNCC.cs            # C#类，声明P/Invoke接口和托管包装 
│   ├── PatternMatchByNCC.cs             # C#类，C#原生实现，托管包装
│   ├── PatternMatchByNCC.csproj         # C#类库项目文件 
│   └── ...                              # 其它C#辅助文件 
│ ├── PatternMatchNative/                # C++核心算法工程 
│   ├── PatternMatchNative.vcxproj       # C++工程文件  
│   ├── include/                         # C++头文件目录 
│   │   └── PatternMatchByNCC.h          # 算法类头文件 
│   └── src/                             # C++源文件目录 
│       └── PatternMatchByNCC.cpp        # 算法实现 
│ ├── Test Images/                       # 测试用图片（需自行准备） 
│   ├── Src4.bmp                         # 源图像 
│   └── Dst4.bmp                         # 模板图像 
│ ├── README.md                          # 项目说明文档 
└── ...                                  # 其它工程相关文件（如.sln、.gitignore等）
```

# Usage

- 接口

  ```C#
  using OpenCvSharp;
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  
  namespace PatternMatch
  {
      /// <summary>
      /// 模板匹配接口，使用归一化互相关(NCC)进行匹配
      /// </summary>
      public interface IPatternMatchByNCC
      {
          /// <summary>
          /// 学习模板图像并构建金字塔
          /// </summary>
          /// <param name="matTemp">输入模板图像</param>
          /// <param name="pyramidMaxLayers">图像金字塔层数</param>
          /// <param name="minReudceArea">最小缩放尺寸</param>
          /// <param name="maxLevelFirst">是否优先使用给定的图像金字塔层数。
          /// true使用给定的图像金字塔层数，
          /// false使用最小缩放尺寸自动计算图像金字塔层数</param>
          /// <param name="debug">是否启动debug。</param>
          void LearnPattern(
              Mat matTemp,
              int pyramidMaxLayers,
              int minReudceArea,
              bool maxLevelFirst,
              bool debug = false);
  
          /// <summary>
          /// 可视化模板图像金字塔
          /// </summary>
          void ShowTemplatePyramid();
  
          /// <summary>
          /// 模板匹配方法，使用归一化互相关(NCC)进行匹配
          /// </summary>
          /// <param name="src">输入源图像</param>
          /// <param name="reverse">输入图像反转</param>
          /// <param name="angleStep">角度步长</param>
          /// <param name="autoAngleStep">是否使用自动计算角度步长</param>
          /// <param name="startAngle">起始角度</param>
          /// <param name="angleRange">角度范围</param>
          /// <param name="scoreThreshold">匹配得分阈值</param>
          /// <param name="maxMatchCount">最大匹配数量</param>
          /// <param name="useSIMD">是否使用SIMD优化</param>
          /// <param name="maxOverlap">最大重叠比率</param>
          /// <param name="subPixelEstimation">是否进行亚像素估计</param>
          /// <param name="fastMode">是否使用快速模式，启用则进行粗匹配，提升速度，牺牲精度</param>
          /// <param name="numWorks">线程数量，默认为0</param>
          /// <param name="debug">是否启用调试</param>
          /// <returns>匹配结果</returns>
          int Match(
              Mat src,
              // TemplData TemplateData, 
              bool reverse,
              double angleStep = 5,
              bool autoAngleStep = false,
              double startAngle = 0,
              double angleRange = 360,
              double scoreThreshold = 0.8, // 匹配分数阈值
              int maxMatchCount = 100, // 最大匹配数量
              bool useSIMD = true, // 是否使用SIMD优化
              double maxOverlap = 0.5, // 最大重叠比例
              bool subPixelEstimation = false, // 是否进行亚像素估计
              bool fastMode = false, // 是否使用快速模式
              int numWorks = 0,
              bool debug = false);
  
          /// <summary>
          /// 可视化匹配结果
          /// </summary>
          /// <param name="image">输入彩色图像</param>
          /// <param name="color">边框颜色</param>
          /// <param name="indexFrontColor">索引字体颜色</param>
          /// <param name="scoreFrontColor">得分字体颜色</param>
          /// <param name="frontScale">字体大小</param>
          /// <param name="thickness">字体和线条粗细</param>
          /// <param name="showIndex">是否显示索引值</param>
          /// <param name="showDirection">是否显示方向箭头</param>
          /// <param name="showMark">是否显示匹配中心标记</param>
          /// <param name="showScore">是否显示匹配得分</param>
          void Visualization(
              Mat image,
              Scalar color,
              Scalar indexFrontColor = default,
              Scalar scoreFrontColor = default,
              double frontScale = 0.5,
              int thickness = 1,
              bool showIndex = true,
              bool showDirection = true,
              bool showMark = true,
              bool showScore = true);
  
          /// <summary>
          /// 保存模板数据到文件
          /// </summary>
          /// <param name="filePath">模板数据文件路径</param>
          /// <returns>是否保存成功，true成功，false失败</returns>
          bool SaveTemplateData(string filePath);
  
          /// <summary>
          /// 从文件加载模板数据
          /// </summary>
          /// <param name="filePath">模板数据文件路径</param>
          /// <returns>是否加载成功，true成功，false失败</returns>
          bool LoadTemplateData(string filePath);
      }
  }
  ```

- 托管版本类

  ```c#
  PatternMatchByNCC
  ```

- 非托管版本

  ```c#
  CPatternMatchByNCC
  ```

# Protobuf 预生成事件

```bat
// C++
if not exist "$(ProjectDir)generated_cpp" (mkdir "$(ProjectDir)generated_cpp")
for %%F in ("$(SolutionDir)proto\*.proto") do ("$(ProjectDir)3rdparty\protobuf_25_8_dynamic\bin\protoc.exe" --proto_path="$(SolutionDir)proto" --cpp_out="$(ProjectDir)generated_cpp" "%%~fF")

// C#
if not exist "$(ProjectDir)generated" (mkdir "$(ProjectDir)generated")
for %%F in ("$(SolutionDir)proto\*.proto") do ( "$(SolutionDir)PatternMatchNative\3rdparty\protobuf_25_8_static_MD\bin\protoc.exe"  --proto_path="$(SolutionDir)proto" --csharp_out="$(ProjectDir)generated" "%%~fF" )

```

# Native 项目第三方依赖库配置

如果无法通过submodule更新第三方库，将以下仓库下载到3rdparty目录下即可。

- **[protobuf_25_8_static_MDd](https://github.com/gitleej/protobuf_25_8_static_MDd)**
- **[protobuf_25_8_static_MD](https://github.com/gitleej/protobuf_25_8_static_MD)**
- [OpenCV4100](https://github.com/gitleej/OpenCV4100)

# Update

- 20250710
  - fix:实际检测到的目标数量大于所需目标数量时，数组越界，抛出异常
  - 采用OpenMP并行实现第二阶段匹配，匹配速度提升5倍。

- 20250703
  - 依赖[protobuf-v25.8](https://github.com/protocolbuffers/protobuf/archive/refs/tags/v25.8.zip)增加模板数据保存和模板数据加载

# Reference

**[Fastest_Image_Pattern_Matching](https://github.com/DennisLiu1993/Fastest_Image_Pattern_Matching)**
