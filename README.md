# Clean Extract

Clean Extract 是一款面向 Windows 的干净解压工具。

程序在解压前分析压缩包内部文件，识别常见广告、推广链接与系统垃圾，并仅将需要保留的内容解压到磁盘。原始压缩包始终不会被修改。

解压后端使用官方 7-Zip 控制台程序，随本软件一并分发，用户无需另行安装 7-Zip。

## 适用场景

从互联网下载的 ZIP、RAR、7z 压缩包中，经常夹带与资源本体无关的文件，例如：

```text
★本站最新网址.url
★更多游戏下载.url
备用网址.url
永久发布页.txt
__MACOSX/
.DS_Store
Thumbs.db
```

Clean Extract 的目标是：选择压缩包后一键完成干净解压。

## 主要功能

- 支持拖放或选择压缩包；格式以捆绑的 7-Zip 为准，常见类型包括 ZIP、RAR、7z
- 解压前读取压缩包目录，按规则将条目分为 Clean、Suspicious、Trash
- Trash 默认不解压，文件不会落到磁盘
- Suspicious 默认保留，以降低误删说明文档或合法链接的风险
- 对体积较小的 `.url`、`.txt`、`.html` 等文件，可在内存中读取内容辅助判断
- 支持加密压缩包，密码仅用于当次操作，不写入日志，也不持久保存
- 解压完成后显示过滤摘要，并可查看每条命中规则的原因
- 支持将指定文件名标记为“以后总是保留”或“以后总是过滤”
- 提供设置界面，可调整过滤开关、关键词与域名列表
- 可安装资源管理器右键菜单；亦可通过命令行处理指定压缩包

## 工作方式

```text
压缩包
  → 7-Zip 列出内部文件
  → CleanerEngine 按规则分类
  → 必要时读取小型文本 / URL 内容
  → 生成排除列表
  → 7-Zip 仅解压需要保留的条目
  → 显示过滤结果
```

分类含义如下：

| 分类 | 默认行为 | 说明 |
| --- | --- | --- |
| Clean | 解压 | 未命中垃圾规则，或置信度不足 |
| Suspicious | 解压 | 存在广告嫌疑，但尚不足以自动丢弃 |
| Trash | 不解压 | 高置信度的广告、推广文件或系统垃圾 |

仅在高置信度时自动过滤；判断不确定时予以保留。程序只是指示 7-Zip 跳过部分条目，不会改写或删除原始压缩包中的数据。

默认输出目录与常见解压软件一致。例如 `D:\Downloads\Game.rar` 会解压到 `D:\Downloads\Game\`。

## 系统要求

- Windows 10 21H2 或更高版本（x64）
- 运行已构建的程序不依赖 Visual Studio
- 从源码构建需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)

## 使用方法

### 图形界面

运行 `CleanExtract.exe` 后，将压缩包拖入窗口或选择文件，然后点击「干净解压」。

解压完成后可以：

- 打开输出文件夹
- 查看被过滤或保留的可疑文件及原因
- 将某个文件名加入始终保留或始终过滤列表

### 命令行

```text
CleanExtract.exe "D:\Downloads\game.rar"
CleanExtract.exe --install-shell
CleanExtract.exe --uninstall-shell
```

`--install-shell` 与 `--uninstall-shell` 用于安装或移除当前用户的资源管理器右键菜单，写入 `HKEY_CURRENT_USER`，不需要管理员权限。安装后，在 ZIP、RAR、7z 等文件上右键可选择「干净解压」。

右键菜单会指向执行安装时的程序路径。若之后移动了 `CleanExtract.exe`，需要重新安装菜单。

也可在程序设置中完成同样的安装与移除。

## 7-Zip 后端

列目录、读取内部文件与真正解压均通过官方 7-Zip 控制台完成。本项目不自行实现 ZIP、RAR 或 7z 编解码。

随软件分发的是 7-Zip 26.02 Windows x64 完整控制台组件：将官方 `7z.exe` 重命名为 `7zz.exe`，并同时附带 `7z.dll`。

7-Zip 的版权、许可证与 unRAR 限制见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 及 `resources/7-Zip-License.txt`。

## 配置

设置界面中的选项会保存到 `%LocalAppData%\CleanExtract\`：

| 文件 | 内容 |
| --- | --- |
| `settings.json` | 过滤开关，例如是否处理 macOS 元数据、是否保留 Suspicious |
| `rules.json` | 评分阈值与广告关键词 |
| `domains.json` | 信任域名、拦截域名、可疑短链域名 |
| `overrides.json` | 用户指定的始终保留 / 始终过滤文件名 |

程序目录下的 `config\` 提供默认配置。用户配置优先于程序内置默认值。

关于域名策略：

- 拦截域名默认空白。程序不会把真实网站预先标记为广告站点
- 信任域名主要用于文档站点与源码托管，降低误过滤官方链接的概率
- 短链域名只作为风险特征参与评分，不会单独导致文件被过滤

日志位于 `%LocalAppData%\CleanExtract\logs\`。日志记录压缩包路径、命中规则、过滤结果与 7-Zip 退出码，不记录密码，也不记录从压缩包中读取的文件正文。

## 许可证

Clean Extract 源码以 MIT License 发布，见 [LICENSE](LICENSE)。

