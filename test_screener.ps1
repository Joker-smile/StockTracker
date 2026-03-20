# 测试选股功能的PowerShell脚本

Write-Host "=== StockTracker 选股功能测试 ===" -ForegroundColor Cyan
Write-Host ""

# 测试1: 编译项目
Write-Host "[1/3] 编译项目..." -ForegroundColor Yellow
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 编译失败！" -ForegroundColor Red
    exit 1
}
Write-Host "✅ 编译成功" -ForegroundColor Green
Write-Host ""

# 测试2: 检查关键文件
Write-Host "[2/3] 检查关键文件..." -ForegroundColor Yellow
$files = @(
    "bin/Release/net9.0/StockTracker.dll",
    "MainWindow.axaml.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "  ✅ $file 存在" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $file 不存在" -ForegroundColor Red
    }
}
Write-Host ""

# 测试3: 代码检查
Write-Host "[3/3] 代码检查..." -ForegroundColor Yellow

# 检查是否有HttpGetWithRetryAsync方法
if (Select-String -Path "MainWindow.axaml.cs" -Pattern "HttpGetWithRetryAsync" -Quiet) {
    Write-Host "  ✅ 重试机制已添加" -ForegroundColor Green
} else {
    Write-Host "  ❌ 重试机制未找到" -ForegroundColor Red
}

# 检查K线解析是否使用TryParse
if (Select-String -Path "MainWindow.axaml.cs" -Pattern "double\.TryParse\(parts\[2\]" -Quiet) {
    Write-Host "  ✅ K线解析已使用TryParse" -ForegroundColor Green
} else {
    Write-Host "  ❌ K线解析未使用TryParse" -ForegroundColor Red
}

# 检查是否跳过日期字段
if (Select-String -Path "MainWindow.axaml.cs" -Pattern "parts\[0\].*日期.*跳过" -Quiet) {
    Write-Host "  ✅ 日期字段处理正确" -ForegroundColor Green
} else {
    Write-Host "  ⚠️  日期字段处理可能有问题" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== 测试完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "建议手动测试步骤：" -ForegroundColor Yellow
Write-Host "1. 运行程序: dotnet run" -ForegroundColor White
Write-Host "2. 右键菜单选择数据源: 东方财富" -ForegroundColor White
Write-Host "3. 执行选股，观察是否正常" -ForegroundColor White
Write-Host "4. 切换到腾讯数据源，再次测试" -ForegroundColor White
Write-Host ""
