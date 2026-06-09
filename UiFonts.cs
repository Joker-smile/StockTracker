using Avalonia.Media;

namespace StockTracker;

public static class UiFonts
{
    public const string UiFamily = "Microsoft YaHei UI, Microsoft YaHei, PingFang SC, Hiragino Sans GB, Noto Sans CJK SC, Noto Sans SC, Segoe UI, Segoe UI Emoji, Segoe UI Symbol, Arial";
    public const string MonoFamily = "Cascadia Mono, Microsoft YaHei UI, Microsoft YaHei, Noto Sans Mono CJK SC, Consolas, Menlo, monospace";

    public static readonly FontFamily Ui = new(UiFamily);
    public static readonly FontFamily Mono = new(MonoFamily);
}
