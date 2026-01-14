# ============================================
# GS ACQU 部署腳本
# ============================================
# 使用方式: .\deploy.ps1 -TargetServer "10.7.0.xxx" -CatID 3
# ============================================

param(
    [Parameter(Mandatory=$true)]
    [string]$TargetServer,
    
    [Parameter(Mandatory=$true)]
    [int]$CatID,
    
    [string]$TargetPath = "D:\ACQU\GS_$CatID",
    [string]$ServiceName = "GS.Acqu.Worker.$CatID",
    [string]$Username,
    [string]$Password
)

$SourcePath = "$PSScriptRoot\..\src\GS.Acqu.Worker\publish"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "GS ACQU 部署作業" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "目標伺服器: $TargetServer"
Write-Host "CatID: $CatID"
Write-Host "目標路徑: $TargetPath"
Write-Host "服務名稱: $ServiceName"
Write-Host "============================================" -ForegroundColor Cyan

# 確認發布資料夾存在
if (-not (Test-Path $SourcePath)) {
    Write-Host "錯誤: 發布資料夾不存在，請先執行 dotnet publish" -ForegroundColor Red
    Write-Host "執行: dotnet publish -c Release -o publish"
    exit 1
}

# 計算 Port
$GrpcPort = 40000 + $CatID
$ApiPort = 41000 + $CatID

Write-Host ""
Write-Host "Port 配置:" -ForegroundColor Yellow
Write-Host "  gRPC: $GrpcPort"
Write-Host "  API:  $ApiPort"
Write-Host ""

# 建立遠端連線
$RemotePath = "\\$TargetServer\D$\ACQU\GS_$CatID"

Write-Host "步驟 1: 複製檔案到 $RemotePath ..." -ForegroundColor Green

# 建立目標資料夾
if (-not (Test-Path $RemotePath)) {
    New-Item -ItemType Directory -Path $RemotePath -Force | Out-Null
}

# 複製檔案
Copy-Item -Path "$SourcePath\*" -Destination $RemotePath -Recurse -Force

Write-Host "步驟 2: 更新設定檔 (CatID=$CatID) ..." -ForegroundColor Green

# 更新 appsettings.json
$ConfigPath = "$RemotePath\appsettings.json"
$Config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$Config.CatID = $CatID
$Config | ConvertTo-Json -Depth 10 | Set-Content $ConfigPath -Encoding UTF8

Write-Host "步驟 3: 安裝 Windows 服務 ..." -ForegroundColor Green

# 使用 sc.exe 建立服務 (需要在目標機器上執行)
$ExePath = "$TargetPath\GS.Acqu.Worker.exe"

Write-Host @"

============================================
請在目標伺服器 ($TargetServer) 執行以下命令:
============================================

# 1. 建立 Windows 服務
sc.exe create $ServiceName binPath= "$ExePath" start= auto DisplayName= "GS ACQU Worker (CatID=$CatID)"

# 2. 設定服務描述
sc.exe description $ServiceName "Genius Sports 資料採集服務 - CatID=$CatID, gRPC=$GrpcPort, API=$ApiPort"

# 3. 啟動服務
sc.exe start $ServiceName

# 4. 檢查服務狀態
sc.exe query $ServiceName

# 5. 防火牆開放 Port
netsh advfirewall firewall add rule name="GS ACQU gRPC $CatID" dir=in action=allow protocol=tcp localport=$GrpcPort
netsh advfirewall firewall add rule name="GS ACQU API $CatID" dir=in action=allow protocol=tcp localport=$ApiPort

============================================
"@ -ForegroundColor Yellow

Write-Host "部署完成！" -ForegroundColor Green
Write-Host ""
Write-Host "健康檢查 URL: http://${TargetServer}:${ApiPort}/health" -ForegroundColor Cyan
