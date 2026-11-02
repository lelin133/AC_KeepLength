# AC KeepLength

Aicomi的BepInEx 6 IL2CPP插件。当前版本 **0.10.1**。
采用[MIT许可证](LICENSE)，Copyright (c) 2026 lelin133。

## 功能

- FixedLengthClip：固定完整长度，使用运行时网格副本动态裁切。
- NativeTipOffset：保留原生伸缩，当前动作观察到缩短后约0.15秒平滑加入末端偏移；
  同动作恢复完整长度仍保持，换动作重新判断。默认偏移0.03，范围±0.05。
- 轴向防翻转与稳定参考，同动作关闭再开启可恢复参考。
  BindPoseFallback为已验证骨架提供首次静止轴分支回退，可独立关闭。
- F6总开关，J切换模式，F1配置面板需使用者另装配置管理器。

固定模式Clipping/Offset默认0.02、范围-0.02～0.05，与NativeTip/Offset独立。
静止长度基准0.1898。手部校正相关模块已移除。

## 安装

当前可运行包：[AC_KeepLength-v0.10.1.zip](artifacts/AC_KeepLength-v0.10.1.zip)。
同目录附SHA256校验文件。DLL与整理时安装版逐字节一致，安装方法见[INSTALL.md](INSTALL.md)。
GitHub发布时可将ZIP上传为Release附件。

## 构建

使用.NET 10 SDK。插件目标net6.0，测试目标net10.0。
依赖来自使用者自己的游戏目录，不随仓库分发：

- BepInEx/core：BepInEx.Core、BepInEx.Unity.IL2CPP、0Harmony、Il2CppInterop.Runtime。
- BepInEx/interop：Assembly-CSharp、ILGLib、Il2Cppmscorlib、UnityEngine.CoreModule、
  UnityEngine.InputLegacyModule。先正常启动已装BepInEx的游戏，生成互操作程序集。

```powershell
./tools/Build.ps1 -GameDir 'D:/Games/Aicomi'
# 或直接构建
dotnet build src/AC_KeepLength.csproj -c Release -p:GameDir='D:/Games/Aicomi'
```

也可设置AICOMI_GAME_DIR环境变量，或把GamePath.example.props复制为
GamePath.local.props再填写本机路径。命令行GameDir优先；本地路径文件已忽略。
使用标准NuGet缓存，不依赖其他本地项目。
首次构建可能需要联网还原.NET 6目标包。

输出：src/bin/Release/net6.0/AC_KeepLength.dll。
构建后运行`./tools/Pack.ps1`生成ZIP和校验文件。安装构建结果时先退出游戏，再执行：

```powershell
./tools/Install.ps1 -GameDir 'D:/Games/Aicomi'
```

安装器检查文件占用并备份旧DLL/配置，仅适用于当前Aicomi环境。

## 测试

以下检查使用数学规则或托管替身，不需要游戏DLL：

```powershell
dotnet run --project tests/PolicyTests.csproj -c Release
dotnet run --project clip-tests/ClipTests.csproj -c Release
dotnet run --project runtime-tests/RuntimeTests.csproj -c Release
```

私有运行采样不包含在本目录。原开发环境九份采样回放通过，但托管替身不能验证
IL2CPP原生调用、动态父级或最终画面；首次回退功能的真实游戏完整验收仍待反馈。
本次独立构建与测试结果见[VALIDATION.md](VALIDATION.md)。

## 范围

仅处理已验证骨骼/父级。静止轴不同的自定义身体可关闭Rotation/BindPoseFallback。
刻意长轴动画可能被稳定参考抑制。FrameTrace诊断默认关闭，文件大小和记录数有上限。
本目录不包含游戏资源、互操作/第三方DLL、个人配置、日志、截图、历史备份或私有采样。

## 许可证

本项目代码采用MIT许可证，允许使用、修改和再分发（包括商业使用），
须保留版权声明及许可证文本。第三方依赖和游戏资源的权利由各自权利人保留，
不因本项目采用MIT而获得额外授权。完整条款见[LICENSE](LICENSE)。
