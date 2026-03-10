@echo off
REM StockTracker Initialization Script
echo Initializing StockTracker project...

echo.
echo Checking .NET SDK...
dotnet --version
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 9.0 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

echo.
echo Restoring NuGet packages...
dotnet restore
if %errorlevel% neq 0 (
    echo WARNING: Some packages may have failed to restore
)

echo.
echo Building project...
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed
    exit /b 1
)

echo.
echo ============================================
echo StockTracker project initialized successfully!
echo ============================================
echo.
echo Available commands:
echo   dotnet run              - Run the application
echo   dotnet build            - Build the project
echo   dotnet publish -c Release -o publish  - Publish release build
echo.
echo Project location: %CD%
echo.
