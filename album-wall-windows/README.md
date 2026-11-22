# 唱片墙 · Windows 11 屏幕保护程序

默认显示 5 行封面，横向按显示区域的宽高比铺满。两侧固定只露半张，中间列完整显示。16:9 画面中间有 8 列完整封面。每 2.6 秒随机翻转一格，单次翻转约 0.35 秒。翻面在屏保内逐帧绘制，系统的「动画效果」开关不会让它跳过翻面。

0.3.0 增加网易云公开歌单的封面导入。已安装旧版时，退出屏保后用新版 AlbumWall.scr 覆盖原路径的文件，现有设置会沿用。

## 在 Windows 11 上试用

1. 把压缩包解压到一个固定文件夹，保留里面的 AlbumWall.scr。随包测试版适用 Windows x64，内置 .NET Desktop 运行组件。
2. 右键 AlbumWall.scr，在经典右键菜单选择「测试」。移动鼠标、按键、点击或触摸可以退出。
3. 要定时启动，在同一菜单选择「安装」，然后在 Windows 的「屏幕保护程序设置」里选中 AlbumWall，设定等待时间并应用。Windows 会通过这里的「预览」和「设置」打开相应入口。
4. 在「设置」里可调整 3–8 行和 1–30 秒的切换间隔，也可以选择存放图片的目录。将 JPEG、PNG、BMP 或 TIFF 放在目录顶层。屏保每次启动最多选取 160 张。

如果要恢复时要求登录，在 Windows 的屏保设置勾选「在恢复时显示登录屏幕」。要卸载，先在同一窗口把屏保切换到「无」或其他屏保，再删除文件。

## 导入封面

1. 在网易云音乐复制一个公开歌单的分享链接。也可以先试公开热歌榜 https://music.163.com/playlist?id=3778678 。
2. 在屏保的「设置」里点「导入网易云歌单…」，粘贴链接并点「读取歌单」。列表按专辑去重，读取进度和未能解析的曲目数会显示在窗口里。
3. 勾选需要的封面并导入。回到设置窗口，确认「显示已导入的网易云封面」已勾选，点「保存」。导入结果会和已选择的本地图片一起使用。

导入只读取公开网页，私密歌单需要换用已公开的分享链接。大歌单的可读曲目数取决于公开网页。图片存在 `%LOCALAPPDATA%\AlbumWall\NeteaseCovers`，屏保从该目录读取已导入的文件，断网时也能显示。

Apple Music 的封面可以先在 Mac 的「音乐」App 中用封面导出工具存成图片，再拷到 Windows，通过「选择文件夹」加载。

## 从源码构建

在 Windows 上安装 .NET 10 SDK，在 PowerShell 里运行：

    .\build.ps1

产物位于 build/win-x64/AlbumWall.scr。Windows on Arm 可运行：

    .\build.ps1 -Runtime win-arm64

## 工程入口

| 文件 | 用途 |
| --- | --- |
| Program.cs、LaunchOptions.cs | 全屏 /s、嵌入预览 /p HWND、设置 /c |
| AlbumWallView.cs、WallpaperLayout.cs | 宽高比排版与换图时序 |
| FlipSurface.cs、FlipProjection.cs | 单格透视投影与双面翻转 |
| CoverCatalog.cs、Artwork/ | 本地图片解码与内置示例 |
| NeteasePlaylist.cs、NeteaseImportWindow.cs、PlaylistCoverStore.cs | 公开歌单读取、封面预览与本地保存 |
| Settings.cs、SettingsWindow.cs | 用户设置，保存在当前用户的本地应用数据目录 |

打包版本在 Linux 上交叉编译。Windows 11 上的原生设置、预览、空闲触发和实际显示效果需实机验收。
