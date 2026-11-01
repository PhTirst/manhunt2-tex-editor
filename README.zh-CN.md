# Manhunt 2 TEX Editor

[English](README.md) | 简体中文

只处理 **Manhunt 2 PC 版 `.tex`** 的 Windows 贴图编辑器。使用 WinForms 界面和 C++ 原生核心，不使用 Qt；不支持一代 TXD 或其他游戏的 TEX。

当前版本为 `0.1.0-rc.1` 发布候选版。合成资源覆盖结构和编解码回归，不代表所有游戏关卡或 mod 都完成了实机验证。

## 普通用户

1. 完整解压 `manhunt2-tex-editor-*-windows-x64.zip`。
2. 双击 `Mh2TexEditor.exe`；Windows 10/11 x64，无需另装 .NET 或 Visual C++ Runtime。
3. 软件可以放在任意有写入权限的目录，不必放入游戏目录。不要只复制 EXE，`mh2core.dll` 和其他 DLL 必须一起保留。
4. 先复制要编辑的 TEX，再通过“打开”载入副本。

本工具包不附带游戏资源、字体或现成汉化贴图，也不会自动覆盖游戏文件。

## 编辑流程

左侧为原始记录顺序的贴图列表，可按名称或格式过滤；中间显示所选贴图，右侧显示名称、尺寸、DDS 格式、mipmap 数量和槽位大小。

1. 选中贴图，导出 PNG。
2. 用自己熟悉的图像编辑软件修改 PNG，保持宽高不变。
3. 使用“替换 PNG”，或按名称从文件夹批量替换。
4. 切换“原始贴图/当前结果”检查修改，查看 RGBA、Alpha、R/G/B 通道和各级 mipmap。
5. 通过“另存为”输出 TEX，重新打开检查后，再自行部署。

支持撤销、重做、单张还原、深浅背景和棋盘背景、滚轮缩放、拖拽平移。批量处理后会刷新预览；处理过程中暂时禁止关闭窗口，避免打断后台操作。

## 同名贴图不是自动重复导入

原版资源可能真实包含多个同名物理记录，界面用 `name [1/2]`、`name [2/2]` 区分，不会按名称合并。

- 导出使用 `name__1.png`、`name__2.png`，分别对应记录。
- 批量替换优先匹配编号文件；`name.png` 则可同时替换所有同名记录。
- 保存按物理记录偏移写入，不会把一条记录的修改错误应用到另一条。

## 边界与限制

- 仅替换已有记录；不新增、删除、重命名记录，也不改变贴图尺寸或压缩格式。
- 替换 PNG 必须与原图尺寸相同。
- 保留原有 DDS 格式、mipmap 数量、TEX 槽位与记录顺序；超出槽位容量会拒绝替换。
- 保存使用临时文件及替换操作，但不提供自动游戏部署或原版备份。始终保留自己的原版副本。
- 界面当前为简体中文；贴图名称不会翻译。中英文说明不代表界面已经支持多语言切换。

## 快捷键

| 快捷键 | 操作 |
| --- | --- |
| Ctrl+O | 打开 TEX |
| Ctrl+Shift+S | 另存为 |
| Ctrl+E | 导出 PNG |
| Ctrl+R | 替换 PNG |
| Ctrl+Z / Ctrl+Y | 撤销 / 重做 |
| Ctrl+0 / Ctrl+1 | 适应窗口 / 1:1 |

## 开发与发布

需要 Windows x64、.NET 10 SDK、CMake 3.24+、Visual Studio 2022 C++ Build Tools、Windows SDK 和 clang-cl。默认使用本机 VS 中的 clang-cl；不依赖旧工具目录或游戏安装目录。

首次构建会从官方仓库下载固定版本的 DirectXTex 和 zlib-ng，并核对 SHA-256；依赖源码与缓存只放在 `out/native/_deps`，不混入本项目源码 ZIP。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

测试全部使用程序生成的小型 TEX/DDS/PNG，覆盖 BGRA、BC3、原始/压缩 TEX、mipmap、PNG 替换、撤销重做、同名记录隔离和保存重开。不需要正版资源文件参与自动测试。

`out/release/` 包含 self-contained 工具 ZIP、纯源码 ZIP 和 `.sha256`；工具包中的 `release-manifest.json` 列出全部文件哈希。打包还会解压校验，并运行不显示窗口的原生依赖自检。

项目结构：`src/Mh2TexEditor` 是界面，`src/native` 是 TEX/DDS 核心和 C ABI，`tests` 是合成回归，`scripts` 是构建发布入口，`cmake/Dependencies.cmake` 固定依赖版本。GitHub Actions 的 Windows Debug/Release 配置已提供，上传后才会执行。

见 [发布检查](docs/RELEASING.md)、[依赖说明](NOTICE.md) 和 [许可证](LICENSE)。
