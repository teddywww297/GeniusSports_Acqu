@echo off
REM ============================================
REM GS ACQU Windows 服務安裝腳本
REM 在目標機器上以管理員身分執行
REM ============================================

SET CATID=%1
IF "%CATID%"=="" (
    echo 使用方式: install-service.bat [CatID]
    echo 範例: install-service.bat 3
    exit /b 1
)

SET SERVICE_NAME=GS.Acqu.Worker.%CATID%
SET INSTALL_PATH=D:\ACQU\GS_%CATID%
SET EXE_PATH=%INSTALL_PATH%\GS.Acqu.Worker.exe
SET /A GRPC_PORT=40000+%CATID%
SET /A API_PORT=41000+%CATID%

echo ============================================
echo GS ACQU 服務安裝
echo ============================================
echo CatID: %CATID%
echo 服務名稱: %SERVICE_NAME%
echo 安裝路徑: %INSTALL_PATH%
echo gRPC Port: %GRPC_PORT%
echo API Port: %API_PORT%
echo ============================================

REM 檢查是否以管理員身分執行
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo 錯誤: 請以管理員身分執行此腳本！
    pause
    exit /b 1
)

REM 停止現有服務 (如果存在)
echo.
echo 停止現有服務...
sc.exe stop %SERVICE_NAME% 2>nul
timeout /t 3 /nobreak >nul

REM 刪除現有服務 (如果存在)
echo 刪除現有服務...
sc.exe delete %SERVICE_NAME% 2>nul
timeout /t 2 /nobreak >nul

REM 建立新服務
echo.
echo 建立服務: %SERVICE_NAME%
sc.exe create %SERVICE_NAME% binPath= "%EXE_PATH%" start= auto DisplayName= "GS ACQU Worker (CatID=%CATID%)"

REM 設定服務描述
sc.exe description %SERVICE_NAME% "Genius Sports 資料採集服務 - CatID=%CATID%, gRPC=%GRPC_PORT%, API=%API_PORT%"

REM 設定失敗後自動重啟
sc.exe failure %SERVICE_NAME% reset= 86400 actions= restart/60000/restart/60000/restart/60000

REM 開放防火牆 Port
echo.
echo 設定防火牆規則...
netsh advfirewall firewall delete rule name="GS ACQU gRPC %CATID%" 2>nul
netsh advfirewall firewall delete rule name="GS ACQU API %CATID%" 2>nul
netsh advfirewall firewall add rule name="GS ACQU gRPC %CATID%" dir=in action=allow protocol=tcp localport=%GRPC_PORT%
netsh advfirewall firewall add rule name="GS ACQU API %CATID%" dir=in action=allow protocol=tcp localport=%API_PORT%

REM 啟動服務
echo.
echo 啟動服務...
sc.exe start %SERVICE_NAME%

REM 檢查狀態
echo.
echo 服務狀態:
sc.exe query %SERVICE_NAME%

echo.
echo ============================================
echo 安裝完成！
echo 健康檢查: http://localhost:%API_PORT%/health
echo ============================================
pause
