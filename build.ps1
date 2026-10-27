#Requires -Version 5.1
<#
    ASU (Alferd Spritesheet Unpacker) 一键构建脚本 created by TRAE

    用法：
        .\build.ps1                          使用 Release 配置构建
        .\build.ps1 -Configuration Debug     使用 Debug 配置构建
        .\build.ps1 -Clean                   先删除所有 bin / obj 再构建
        .\build.ps1 -Run                     构建成功后直接启动程序

    双击 build.bat 也可以直接构建。
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

$root     = $PSScriptRoot
$solution = Join-Path $root 'SpriteSheetUnpacker.sln'
$config   = $Configuration
$outDir   = Join-Path $root "ASU\bin\$config"
$exePath  = Join-Path $outDir 'ASU.exe'

Set-Location $root

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Write-Fail([string]$Text) {
    Write-Host ''
    Write-Host "[失败] $Text" -ForegroundColor Red
}

# ---------- 1. 检查 .NET SDK ----------
Write-Step '检查 .NET SDK'
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Fail '未找到 dotnet 命令，请先安装 .NET 8 SDK：https://dotnet.microsoft.com/download'
    exit 1
}
Write-Host "SDK 版本: $(dotnet --version)"

# ---------- 2. 可选清理 ----------
if ($Clean) {
    Write-Step '清理 bin / obj'
    # 先完整收集再删除，避免边枚举边删除导致的路径失效
    $dirs = @(Get-ChildItem -Path $root -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq 'bin' -or $_.Name -eq 'obj' } |
        Sort-Object { $_.FullName.Length } -Descending)
    foreach ($dir in $dirs) {
        Write-Host "  删除 $($dir.FullName)"
        Remove-Item -LiteralPath $dir.FullName -Recurse -Force
    }
    Write-Host "共删除 $($dirs.Count) 个目录"
}

# ---------- 3. 还原 NuGet 包 ----------
Write-Step "还原 NuGet 包 ($config)"
dotnet restore $solution
if ($LASTEXITCODE -ne 0) {
    Write-Fail "还原失败 (退出码 $LASTEXITCODE)"
    exit $LASTEXITCODE
}

# ---------- 4. 构建 ----------
Write-Step "构建 ($config)"
dotnet build $solution -c $config --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Fail "构建失败 (退出码 $LASTEXITCODE)"
    exit $LASTEXITCODE
}

# ---------- 5. 结果 ----------
Write-Step '构建成功'
if (-not (Test-Path $exePath)) {
    Write-Fail "未找到输出文件: $exePath"
    exit 1
}
Write-Host "可执行文件: $exePath" -ForegroundColor Green
Write-Host "导出目录  : $(Join-Path $outDir 'output')" -ForegroundColor Green

# ---------- 6. 可选启动 ----------
if ($Run) {
    Write-Step '启动程序'
    Start-Process -FilePath $exePath
}

exit 0