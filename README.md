# AlbumWall 唱片墙

AlbumWall 是一个用 C# / WPF 编写的 Windows 11 屏幕保护程序。它把专辑封面铺满屏幕，并定时挑一张沿竖轴翻面换图。

## 功能

- 默认 5 行，列数随显示区域的宽高比变化。16:9 下中间有 8 个完整列，左右各显示半列。
- 默认每 2.6 秒翻转一张，单次约 0.35 秒。可在设置里调整为 3–8 行和 1–30 秒的切换间隔。
- 支持本地图片和网易云公开歌单。两种来源可以同时启用。屏保启动后直接读取本机封面，断网也可显示。
- 支持 Windows 原生屏保设置、预览和空闲启动。全屏显示时移动鼠标、按键、点击或触摸即可退出。

## 安装和使用

1. 解压 Windows x64 发布包 `AlbumWall-Windows11-x64.zip`。右键 `AlbumWall.scr`，在经典菜单里选 **测试**。发布包已包含运行所需的 .NET 组件。
2. 要随系统空闲启动，在同一菜单选 **安装**，再到 **屏幕保护程序设置** 选择 AlbumWall，设置等待时间并应用。
3. 点 **设置** 可以调整行数、换图间隔和封面来源。升级时可用新版 `.scr` 替换原路径的文件。用户设置和已导入的封面会保留。

### 添加封面

| 来源 | 操作 |
| --- | --- |
| 本地图片 | 在设置里点 **选择文件夹**。把 JPG、PNG、BMP 或 TIFF 放在文件夹顶层。屏保每次启动最多选取 160 张。 |
| 网易云音乐 | 点 **导入网易云歌单…**，粘贴公开歌单分享链接。读取后预览并勾选封面，导入再保存设置。同一专辑 ID 只导入一张。 |
| Apple Music | 先在 Mac 上将封面导出为图片，拷到 Windows，再按本地图片的方式添加。 |

用户设置位于 `%LOCALAPPDATA%\AlbumWall\settings.json`。网易云导入的图片位于 `%LOCALAPPDATA%\AlbumWall\NeteaseCovers`。

## 源码构建

在 Windows 上安装 .NET 10 SDK，从仓库根目录用 PowerShell 运行：

```powershell
.\album-wall-windows\build.ps1
```

默认生成 `album-wall-windows/build/win-x64/AlbumWall.scr`。为 Windows on Arm 构建时，在命令末尾加 `-Runtime win-arm64`。

## 工作区文件

| 路径 | 用途 |
| --- | --- |
| [album-wall-windows/](album-wall-windows/) | WPF 源码、内置示例封面、构建脚本和随发布包附带的使用说明。 |
| [album-wall-preview/album-wall.html](album-wall-preview/album-wall.html) | 可在浏览器打开的动画原型，用于预览排版和翻转。 |
| `album-wall-preview/album-wall-demo.mp4` | 校准后的 10 秒动画演示。本地参考文件。 |
| `upload/IMG_4940.MOV` | macOS 屏保实拍，用于对照半列裁切与换图节奏。本地参考文件。 |
| `album-wall-test-covers/`、`AlbumWall-Test-Covers.zip` | 导入测试用的《1989》和《Justice》封面。本地测试素材。 |
| `AlbumWall-Windows11-x64.zip` | Windows x64 发布包，内含 `.scr`、说明和源码。 |
| `album-wall-windows/bin/`、`obj/`、`build/` | .NET 中间文件及构建输出。 |

### 源码入口

| 文件 | 用途 |
| --- | --- |
| [Program.cs](album-wall-windows/Program.cs)、[LaunchOptions.cs](album-wall-windows/LaunchOptions.cs) | 全屏 `/s`、设置 `/c` 和嵌入式预览 `/p`。 |
| [AlbumWallView.cs](album-wall-windows/AlbumWallView.cs)、[WallpaperLayout.cs](album-wall-windows/WallpaperLayout.cs)、[FlipProjection.cs](album-wall-windows/FlipProjection.cs) | 封面墙排版、换图节奏和翻面投影。 |
| [SettingsWindow.cs](album-wall-windows/SettingsWindow.cs)、[Settings.cs](album-wall-windows/Settings.cs)、[CoverCatalog.cs](album-wall-windows/CoverCatalog.cs) | 设置界面、设置持久化和本地图片加载。 |
| [NeteasePlaylist.cs](album-wall-windows/NeteasePlaylist.cs)、[NeteaseImportWindow.cs](album-wall-windows/NeteaseImportWindow.cs)、[PlaylistCoverStore.cs](album-wall-windows/PlaylistCoverStore.cs) | 读取公开歌单，预览、勾选并保存封面。 |
