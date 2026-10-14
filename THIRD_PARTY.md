# 第三方组件

本项目的 OpenCV 处理使用 OpenCvSharp4 4.9.0.20240103，以及配套的 Windows native runtime。运行包保留 NuGet 提供的运行时文件结构。

- [OpenCvSharp 官方项目](https://github.com/shimat/opencvsharp)：Apache-2.0。
- [OpenCvSharp4 固定版本](https://www.nuget.org/packages/OpenCvSharp4/4.9.0.20240103)。
- [OpenCvSharp4.runtime.win 固定版本](https://www.nuget.org/packages/OpenCvSharp4.runtime.win/4.9.0.20240103)。
- [OpenCV 项目与许可证说明](https://opencv.org/license/)。
- runtime 包含 OpenCV 的 FFmpeg videoio 插件，本版仅处理静态图片；上游信息见 [OpenCV 视频接口](https://docs.opencv.org/4.x/d0/da7/videoio_overview.html) 与 [FFmpeg 许可证说明](https://ffmpeg.org/legal.html)。
- 单元断言使用 [xUnit](https://github.com/xunit/xunit)，验证源码按 NuGet 还原相应测试依赖；测试框架不包含在桌面 app 中。

当前交付用于个人运行和源码学习。对外分发运行包时，应保留并核对各依赖版本的完整许可证、版权声明及相应要求。
