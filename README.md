# StockTracker 📈
> **潜行盯盘工具 - 大A定制版 (Avalonia UI / .NET 9.0)**

StockTracker 是一款专为A股市场定制的**极致隐蔽、智能高效**的实时盯盘利器。它在保持水印级极简 UI 设计的同时，内置了深度适配大 A 盘口特征的“智测量化引擎”以及**真正的零依赖独立选股系统**。

---

## 📸 效果展现

![StockTracker 预览](assets/preview.png)

> **设计理念**：极致的透明度与暗色调，使其能完美融入 VS Code、PyCharm、IntelliJ IDEA 等主流开发工具的深色背景。全程静默，毫无破绽。

---

## 📦 终极绿色版 (True Standalone)
本项目经过底层重构，已经实现真正的**单文件绿色版**。
- 解压即用，无任何其他文件（没有脚本、没有配置文件依赖）。
- **内置运行时，无需额外安装 .NET 或 Python**。所有核心量化逻辑均由 C# 原生运行。

| 平台 (Platform) | 下载与运行 (Direct Run) | 说明               |
| :--- | :--- |:-----------------|
| **🪟 Windows (64位)** | [下载 StockTracker.exe](https://github.com/Joker-smile/StockTracker/raw/master/publish/win-x64/StockTracker.exe) | 桌面端直接双击运行，配置随身携带        |
| **🍎 macOS (M系列芯片)** | [下载 StockTracker](https://github.com/Joker-smile/StockTracker/raw/master/publish/osx-arm64/StockTracker) | Apple Silicon (M1/M2/M3/M4) 完美运行        |
| **🍎 macOS (Intel芯片)** | [下载 StockTracker](https://github.com/Joker-smile/StockTracker/raw/master/publish/osx-x64/StockTracker) | Intel 芯片 Mac 支持  |

> [!IMPORTANT]
> **🍎 macOS 用户必看！如何防止双击打开黑框 (Terminal)？**
> 
> 下载到的 `StockTracker` 是一个纯 Unix 二进制可执行文件。在 Mac 上直接双击这种文件，系统默认会先弹出一个 Terminal (终端) 黑框来承载它。
> 
> **一分钟完美包装法（伪装成原生应用，彻底干掉黑框）：**
> 1. 在桌面新建一个文件夹，命名为 `StockTracker.app`（加了 .app 后缀立刻会变成正规软件的图标形状）。
> 2. 右键这个 `StockTracker.app`，选择 **显示包内容**。
> 3. 在里面新建文件夹，命名为 `Contents`。
> 4. 进入 `Contents`，再新建文件夹，命名为 `MacOS`。
> 5. 把下载好的那个文件拖进 `MacOS` 文件夹里。
> 6. *可选：通过终端赋予权限 `chmod +x StockTracker.app/Contents/MacOS/StockTracker`*
> 
> 现在，直接双击桌面的 `StockTracker.app`，它就会像正规 macOS 软件一样安静地弹出图形界面，绝无黑框打扰！
> 
> **关于运行报错提示**：如果提示“未知开发者”或文件损坏，请在系统设置 -> 隐私与安全性中允许运行，或终端执行 `xattr -cr /路径/StockTracker.app` 绕过沙盒。

---

## 🧠 Deep-A 智测逻辑 (Algorithm Overview)

内置的“智测”不仅判断涨跌，而是通过一套专为 A 股打造的短线资金量化模型，给出现象级的**盘口定性**。其核心观测指标与推演逻辑如下：

### 1. 核心数据源 (Data Dimensions)
-   **时间加权量比 (Time-Weighted Volume Ratio)**：精确计算当前分钟的成交手数，并按全天 240 分钟的基础时间进度，动态对比过去 5 个交易日的平均成交量。这能第一时间发现“早盘异动爆量”或“午后缩量断层”。
-   **MA5 偏离度 (MA5 Deviation)**：实时计算当前价格与 5 日均线（短期成本线）的乖离率。判断股价处于“强势逼空”（偏离度 > 1%）还是“弱势破位”。
-   **日内 K 线解剖 (Intraday Candlestick Analysis)**：拆解当前价格、开盘价、最高价、最低价。精准测量实体大小 (Body Size)、上影线 (Upper Shadow) 与下影线 (Lower Shadow) 的比例关系。

### 2. 经典盘口定性模型 (Signal Scenarios)

智测算法通过组合上述维度，触发以下判定池（摘要）：

-   **🎯 涨跌停封板探测**：
    -   识别到触及涨停价且持续监控量比。若 `量比 < 0.6` 判定为 **“极度控盘(缩量一字板)”**；若 `量比 > 2.0` 提示 **“爆量打板(分歧大/防炸)”**。
-   **🌡️ 关键影线洗盘/逃顶识别**：
    -   **仙人指路 (Bullish Upward Test)**：收红盘，处于五日线上方的多头趋势，且出现长上影线且量比适中。
    -   **金针探底 (Bullish Pin Bar)**：出现极长的下影线，伴随 `量比 > 1.5` 的底部爆量承接，提示洗盘可能结束。
    -   **避雷针 (Bearish Pin Bar)**：放量且收绿的长上影线，直接提示 **“[看空]抛压巨大”**。
-   **⚖️ 常态量价推演**：
    -   多头区间：低量比上涨定义为 **“缩量逼空(控盘)”**，高量比爆拉定义为 **“爆量主升”**。
    -   空头区间：重点防御缩量下的 **“阴跌不止”** 与放量的 **“破位杀跌”**。

---

## 🚀 核心架构与全新优势

1.  **真正的“潜行”体验**：
    -   **无边框、无标题栏**：去除一切系统原生装饰。
    -   **水印级透明**：背景半透明，完美融入桌面，不占焦、不霸屏。
    -   **工业级完美对齐**：采用原生宏观栅格 (Monolithic Grid)，所有数值列（价格、涨幅、盘口）均能随文字长度自动达成像素级垂直右对齐。

2.  **极度稳健的抗崩溃绿色存储**：
    -   **优先纯绿色行为**：在有权限的环境（如 Windows 桌面），你的配置 `stocks.txt` 和错误日记 `error_log.txt` 都会紧挨着程序生成，拔走U盘就能带走。
    -   **智能降级防御**：如果软件被放进受限目录（如 macOS 的 /Applications 或 Win 的 Program Files），它会静默拦截异常，退保到系统安全目录 (`%APPDATA%` 或 `~/.config/`)，**绝不闪退**。任何网络或接口 API 的深层错误也会被详细输出极客级的排错日志。

2.  **独创 Deep-A 智测逻辑与定性**：
    -   结合实时**量比**、**MA5 乖离**、**盘内 K 线影线比例**，实盘秒级定位 [看多]、[观察]、[看空]。
    -   系统能准确吐出资金切片情绪：如“爆量打板”、“仙人指路”、“金针探底”、“缩量逼空”、“破位杀跌”等专业盘口术语。

3.  **100% Native 原生独立量化选股**：
    -   一键启动纯 C# 本地版量化选股引擎，无黑框。
    -   **多数据源灾备**：支持在“东方财富”与“腾讯”数据源间手动切换。当主接口遭到 IP 封锁或网络波动时，可无缝切换备用通道。
    -   **工业级反爬策略**：内置浏览器级 Stealth Headers (User-Agent 伪装) 与随机动态节流延迟，有效降低被大数据风控封锁 IP 的风险。
    -   **复权一致性**：自动匹配前复权 (QFQ) K 线数据进行均线建模，确保选股规则在所有数据源下逻辑完全对齐。
    -   自动精筛市盈率、百亿盘子、以及换手率爆发个股，分析 MA20/MA200 形态，自动同步至盯盘界面。

---

## 🖱️ 交互快捷指南

-   **移动窗口**：左键点击界面**任意位置**并按住即可拖移。
-   **右键控制核心菜单**：
    -   **🪄 自动选股**：一键唤起纯 Native C# 并发量化机制，扫盘 A 股。全程无黑框跳出，UI 进度条友好展示，随后热重载推送。
    -   **📊 选股数据源**：支持手动指定“东方财富”或“腾讯”作为选股接口，并以“√”标识当前活动源。
    -   **➕ 添加股票**：输入股票代码即可添加。支持 ETF、可转债、A 股全市场。
    -   **🗑️ 删除个股**：在对应的股票行上右键精确删除。
    -   **♻️ 清空全部**：清空所有代码列表，重新开始。
-   **隐藏控制面板**：鼠标悬停在右上角才会显现关闭与最小化按钮。

---

## 🛠️ 纯血 C# 构建说明 (Developer Info)

-   **Runtime**: .NET 9.0 (无须系统安装框架，打包成 Self-Contained)
-   **UI Engine**: Avalonia UI 11.0
-   **Graphics**: SkiaSharp (已内置 macOS 原生渲染加速动态库)

### 极速编译部署方案 (init.bat)：
项目根目录自带 `init.bat` 构建脚本。双击运行将自动完成：
1. 依赖还原 (NuGet Restore)
2. MSBuild 单文件 (Single-File) 发布
3. **一次性编译三个平台版本：** `Win x64`, `macOS x64` (Intel), 和 `macOS arm64`。

*原生手动构建命令示例：*
```powershell
# Windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64

# macOS Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o publish/osx-arm64

# macOS Intel
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o publish/osx-x64
```

---

## 📄 开源说明
本项目仅供学习 Avalonia UI 跨平台开发与 C# 量化逻辑研究使用，数据均来自外部第三方接口（新浪财经、东方财富等），不构成任何投资建议。
市场有风险，投资需谨慎。入市有风险，摸鱼需低调。
