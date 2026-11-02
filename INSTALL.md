# AC KeepLength 0.10.1 安装说明

前提：已能正常运行的Aicomi和BepInEx 6 IL2CPP。依赖由使用者自行安装，
本包不附带游戏文件、BepInEx或其他第三方DLL。

1. 退出游戏。
2. 将包内BepInEx文件夹解压合并到Aicomi.exe所在目录。
3. 最终路径：BepInEx/plugins/AC_KeepLength/AC_KeepLength.dll。
4. 启动游戏。F1界面需要已安装可用的配置管理器。

F6为总开关，J切换模式，均可改键。配置位于
BepInEx/config/local.ac.keeplength.cfg，首次运行生成。

- FixedLengthClip：固定长度和动态裁切；Clipping/Offset默认0.02。
- NativeTipOffset：保留原生伸缩并偏移末端；NativeTip/Offset默认0.03。

不要在plugins下重复放置同名插件。升级覆盖同一路径DLL并保留配置。
旧配置中废弃键不会被当前DLL读取；需清理时先备份，再使用源码的安装脚本。
停用可用F6；卸载需退出游戏后移除插件DLL。

这是当前0.10.1已构建版本；DLL与整理时安装版一致。离线检查通过不等于全部
身体/动作/第三方插件组合已验收，自定义骨架需要另行适配。
