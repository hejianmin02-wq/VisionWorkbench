# v0.2 ROI 使用与验证

验证日期：2026-10-15，Asia/Shanghai。

## 使用

1. 加载示例，或打开本地图片。
2. 勾选“启用 ROI 区域检测”。X、Y 为区域左上角，宽、高为正整数，单位为原图像素。
3. 输入区域，观察原图中的琥珀色虚线框，再点击“执行检测”。
4. 表格 X、Y 已还原到原图坐标，二值图保持原图尺寸，区域外为黑色。
5. 保存结果，得到标注 PNG、全尺寸二值 PNG 与 SchemaVersion 2 的测量 JSON。

默认关闭 ROI，保持整图处理。关闭时 Parameters.Roi 为 null，Region 为整图。反转与高斯模糊也只作用于区域内部。

“ROI 设为整图”使用当前图片实际尺寸。修改参数后保留旧结果，参数不匹配时不能保存。非整数、负坐标、空区域、越界区域都会被拒绝。已有 ROI 不适合新图片时，该次处理失败并保留旧结果；可关闭 ROI 后重新加载。

本版采用数值输入。鼠标拖拽、旋转区域、实时相机与物理标定属于后续功能。

## 实际结果

![实际 WPF ROI 窗口渲染](workbench-roi.png)

960×600 合成图，阈值 128、最小面积 500，ROI 为 `(80,160,260,220)`：只保留一个矩形，原图包围框为 `(100,185,220,170)`，面积 37011 px²。关闭 ROI 后恢复三个大轮廓。

截图来自发布程序的 WPF 可视树离屏渲染，不能替代不同 DPI、键鼠与文件对话框的人工检查。显示耗时是单次观察值，包含 PNG 编码，不作为性能基准。

## 坐标与边界

右、下边界为排他边界。要求 X、Y 非负，宽、高为正，且 `X + Width <= 图片宽`、`Y + Height <= 图片高`；校验使用 long 加法，避免 int 溢出。

轮廓提取传入偏移 `(X,Y)`，使轮廓点、包围框与绘制位置使用原图坐标。处理从区域子矩阵开始，原始 PNG 保持输入内容。相关接口见 [OpenCV Mat](https://docs.opencv.org/4.x/d3/d63/classcv_1_1Mat.html) 与 [findContours](https://docs.opencv.org/4.x/d3/dc0/group__imgproc__shape.html)。

跨界目标按截取后的形状测量。例如，30×30 矩形只剩下 20×20 区域时，轮廓面积为 361 px²。导出的 RoiBoundaryPolicy 记录该语义。

## 验证结果

- Release 解决方案构建成功：0 警告，0 错误。
- 单进程 Fact/InlineData 断言：Passed 36，Failed 0；原有 15 项与 ROI 新增 21 项。记录见 [in-process-verification.json](in-process-verification.json)。
- 发布目录 WPF 程序：退出码 0，Passed 21，Failed 0；覆盖参数过期、坐标与框显示、非法输入、失败保留、整图恢复及整图按钮。记录见 [smoke-test.json](smoke-test.json)。
- WPF 绑定错误日志为空，实际截图已查看。
- 标准 VSTest：等待 testhost 连接 90 秒后中止，未获得成功的标准适配器运行结果。36 项通过的是单进程断言结果。

## 验证命令

还原依赖后，本次构建命令：

```powershell
dotnet build VisionWorkbench.sln --configuration Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

本次尝试的标准测试命令（制作环境未完成）：

```powershell
dotnet test tests/VisionWorkbench.Tests/VisionWorkbench.Tests.csproj --configuration Release --no-build --no-restore --logger "trx;LogFileName=roi.trx" --results-directory artifacts/standard-tests -m:1 -nr:false
```

单进程断言与 WPF 流程验证入口：

```powershell
dotnet run --project tools/VisionWorkbench.Verify/VisionWorkbench.Verify.csproj --configuration Release -- artifacts/verification
dotnet run --project src/VisionWorkbench.Desktop/VisionWorkbench.Desktop.csproj --configuration Release -- --smoke-test artifacts/verification
```

制作环境使用既有离线 NuGet 缓存，设置 MSBuildEnableWorkloadResolver=false，并使用单节点完成还原与编译。正常开发环境按根 README 的常规命令运行。

