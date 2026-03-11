# StockTracker 📈
> **潜行盯盘工具 - 大A定制版 (Avalonia UI / .NET 9.0)**

StockTracker 是一款专为中国 A 股市场定制的**极致隐蔽、智能高效**的实时盯盘利器。它在保持水印级极简 UI 设计的同时，内置了深度适配大 A 盘口特征的“智测量化引擎”。

---

## 📸 效果展现

![StockTracker 预览](assets/preview.png)

> **设计理念**：极致的透明度与暗色调，使其能完美融入 VS Code、PyCharm、IntelliJ IDEA 等主流开发工具的深色背景。

---

## 📦 绿色版快速运行 (Direct Run)
本项目已预编译好对应平台的**单文件绿色版**，解压即用，**内置运行时，无需额外安装 .NET**。

| 平台 (Platform) | 下载与运行 (Direct Run) | 说明               |
| :--- | :--- |:-----------------|
| **🪟 Windows (64位)** | [下载 StockTracker.exe](https://github.com/Joker-smile/StockTracker/raw/master/publish/win-x64/StockTracker.exe) | 桌面端直接双击运行        |
| **🍎 macOS (M系列芯片)** | [下载 StockTracker](https://github.com/Joker-smile/StockTracker/raw/master/publish/osx-arm64/StockTracker) | Apple M系列        |
| **🍎 macOS (Intel芯片)** | [下载 StockTracker](https://github.com/Joker-smile/StockTracker/raw/master/publish/osx-x64/StockTracker) | Intel 芯片 Mac 支持  |

> [!IMPORTANT]
> **macOS 用户须知**：
> 1. **双击提示“未能打开文稿...文本编码 Unicode (UTF-8) 不适用”**：这是因为在 Windows 平台上编译并拷贝过去后，Unix 系统的“可执行权限”丢失了，Mac 把它当成了普通文本文档。
>    **解决方法**：打开系统自带的 `终端 (Terminal)`，输入命令 `chmod +x `（注意 `+x` 后面有一个空格），然后把 `StockTracker` 文件拖入终端窗口，按下回车键赋予权限，即可正常双击运行。
> 2. **首次运行提示保障安全/未签名**：由于未向苹果交保护费签名，请在 Finder 中对准文件**右键点击并选择“打开”**，或者在“系统偏好设置 -> 隐私与安全性”中点击“仍要打开”。

---

## 🚀 核心优势

1.  **真正的“潜行”体验**：
    -   **无边框、无标题栏**：去除一切系统原生装饰，仅保留纯净的数据行。
    -   **水印级透明**：背景半透明（约 60% 透明度），字体色值经过精准调校，如同桌面水印。
    -   **置顶不占焦**：始终悬浮在顶层，但不干扰当前的业务流程。
2.  **工业级完美对齐 (Monolithic Grid)**：
    -   底层采用原生宏观栅格 (Monolithic Grid) 架构。
    -   无论股票名称采用汉字、字母组合何种长度，所有数值列（价格、涨幅、盘口）均能在垂直方向实现像素级完美的右对齐，带来极度舒适的表格化观感。
3.  **独创 Deep-A 智测逻辑**：
    -   结合实时**量比**、**MA5 均线**、**盘口分时趋势**进行综合评估。
    -   自动识别[看多]、[观察]、[看空]状态，并给出“爆量主升”、“缩量洗盘”、“震荡试盘”等专业盘口定性。
4.  **全平台原生性能**：
    -   基于最新的 **Avalonia UI 11.0** 重构，C# 实现，内存占用极低（仅需约 30MB-50MB）。
5.  **实时高频刷新与热重载**：
    -   默认 3 秒级别的高频率数据采集，对接稳定的新浪财经实时行情接口。
    -   底层搭载 `FileSystemWatcher`，盯盘配置 (`stocks.txt`) 发生任何外部修改时（例如被量化脚本覆写），UI 将在毫秒级无缝自动刷新，**全程免重启**。
6.  **内置 Python 强力选股骨干 (全新集成)**：
    -   系统原生集成了基于东方财富接口的 A 股极速选股引擎 (StockScreener)。
    -   一键启动，自动执行复杂的过滤条件（MA20趋势、PE<40、20-500亿精选盘子、近期换手率异动脉冲等）。
    -   选出的高弹性“妖股苗子”会自动打通热重载管线，直接同步至盯盘终端！

---

## 🖱️ 交互快捷指南

-   **移动窗口**：左键点击界面**任意位置**并按住不放即可随意拖动位置。
-   **右键控制核心**：
    -   **自动选股 (Auto Pick)**：一键唤醒内置的 Python 量化选股引擎，深度扫描 A 股全市场，寻找具备爆发潜力的黑马！
        -   **静默护航**：后台开启纯静默进程，无任何弹窗打扰。
        -   **防封锁机制**：点击后触发防连点锁定，UI 居中显示高亮的 `⏳ 正在全网寻妖(约需15秒)...`。
        -   **无缝衔接**：选完后通过热重载机制（自动分发到三端配置），免重启直接将新鲜的强势股推送到盯盘视野！
    -   **添加股票**：输入 6 位数字代码或 sh/sz 前缀代码。支持股票、ETF、可转债。
    -   **删除个股**：在对应的股票行情行点击右键，精准删除个股。
    -   **清空全部**：一键重置自选列表。
-   **控制按钮**：右上角隐藏式三个控制键（最小化、关闭）。

---

## 🛠️ 技术底座与构建 (Developer Info)

-   **Runtime**: .NET 9.0 (Standard)
-   **UI Engine**: Avalonia UI 11.0 (Skia 渲染)
-   **Data Flow**: Sina Finance API / GB2312 编码处理

### 极速编译部署方案 (init.bat)：
我们提供了全新的生命周期管理脚本 `init.bat`。只需在根目录双击运行，它将会全自动：
1. 检查并验证 .NET SDK 环境。
2. 还原 NuGet 所有依赖包。
3. 执行 Release 整体编译构建。
4. **一键并行输出 Win x64, macOS x64 和 macOS arm64 三个平台的独立绿色文件！** 输出的单文件免安装包将存放于 `publish/` 目录下。

*当然，您也可以手动敲击编译命令进行局部发布：*
```powershell
# Windows
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64

# macOS Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o publish/osx-arm64

# macOS Intel
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o publish/osx-x64
```

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

## 📄 开源说明
本项目仅供学习交流使用，数据均来自外部第三方接口，不构成任何投资建议。
市场有风险，投资需谨慎。入市有风险，摸鱼需低调。
