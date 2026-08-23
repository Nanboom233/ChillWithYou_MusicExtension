## MusicBridge {{hash}}

本包由开源源码构建，包含 BepInEx 运行时和插件本体。

### 安装
1. 解压到游戏根目录。根目录内有 Chill With You.exe。
2. 通过 Steam 启动游戏。

### 包内内容
| 路径 | 说明 |
|---|---|
| winhttp.dll、doorstop_config.ini | BepInEx {{bepinex}} 加载器 |
| BepInEx/core/* | BepInEx 运行时（含 HarmonyX） |
| BepInEx/plugins/ChillWithYouMusicBridge/MusicBridge.Plugin.dll | 插件本体，由 CI 从源码构建 |

### 注意
- 需要 Steam 正版环境。
- 反馈问题时请附上 BepInEx/LogOutput.log。

**Full Changelog**: https://github.com/{{REPO}}/compare/{{base}}...{{hash}}
