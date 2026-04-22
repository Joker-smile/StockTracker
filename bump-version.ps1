# StockTracker Robust Version Bumping Script
# Explicitly uses UTF-8 without BOM to avoid encoding issues.

$programPath = "Program.cs"
$readmePath = "README.md"

try {
    $utf8NoBOM = New-Object System.Text.UTF8Encoding($false)

    if (-not (Test-Path $programPath)) {
        Write-Error "Program.cs not found"
        exit 1
    }

    $content = [System.IO.File]::ReadAllText($programPath, $utf8NoBOM)
    if ([string]::IsNullOrWhiteSpace($content)) {
        Write-Error "Program.cs is empty"
        exit 1
    }

    if ($content -match 'APP_VERSION = "(v\d+\.\d+\.\d+)"') {
        $oldVersion = $matches[1]
        
        if ($oldVersion -match 'v(\d+)\.(\d+)\.(\d+)') {
            $x = [int]$matches[1]
            $y = [int]$matches[2]
            $z = [int]$matches[3]
            
            $z++
            if ($z -ge 10) {
                $z = 0
                $y++
                if ($y -ge 10) {
                    $y = 0
                    $x++
                }
            }
            
            $newVersion = "v$x.$y.$z"
            Write-Host ">>> Current: $oldVersion"
            Write-Host ">>> Target: $newVersion"
            
            $newContent = $content.Replace("APP_VERSION = `"$oldVersion`"", "APP_VERSION = `"$newVersion`"")
            if ($newContent -ne $content) {
                [System.IO.File]::WriteAllText($programPath, $newContent, $utf8NoBOM)
                Write-Host ">>> [Success] Program.cs updated"
            }

            if (Test-Path $readmePath) {
                $readmeContent = [System.IO.File]::ReadAllText($readmePath, $utf8NoBOM)
                if (-not [string]::IsNullOrWhiteSpace($readmeContent)) {
                    $newReadme = $readmeContent -replace "Current Version: $oldVersion", "Current Version: $newVersion"
                    if ($newReadme -ne $readmeContent) {
                        [System.IO.File]::WriteAllText($readmePath, $newReadme, $utf8NoBOM)
                        Write-Host ">>> [Success] README.md updated"
                    }
                }
            }
        }
    } else {
        Write-Warning ">>> [Error] APP_VERSION not found in Program.cs"
    }
} catch {
    Write-Error ">>> Exception: $_"
    exit 1
}
