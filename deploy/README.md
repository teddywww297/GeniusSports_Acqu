# GS ACQU 部署指南

## 快速部署步驟

### 1. 本機發布
```powershell
cd C:\2025Teddy\程式\Genius\src\GS.Acqu.Worker
dotnet publish -c Release -o publish
```

### 2. 複製到目標機器
```powershell
# 複製到遠端機器 (範例: CatID=3 籃球)
$TargetServer = "10.7.0.xxx"
$CatID = 3
$RemotePath = "\\$TargetServer\D$\ACQU\GS_$CatID"

# 建立資料夾並複製
New-Item -ItemType Directory -Path $RemotePath -Force
Copy-Item -Path ".\publish\*" -Destination $RemotePath -Recurse -Force
```

### 3. 更新設定檔
編輯 `D:\ACQU\GS_3\appsettings.json`，確認 `CatID` 正確：
```json
{
  "CatID": 3,
  ...
}
```

### 4. 在目標機器安裝服務
以管理員身分執行：
```batch
D:\ACQU\GS_3\install-service.bat 3
```

或手動執行：
```batch
sc.exe create GS.Acqu.Worker.3 binPath= "D:\ACQU\GS_3\GS.Acqu.Worker.exe" start= auto
sc.exe start GS.Acqu.Worker.3
```

---

## Port 對照表

| CatID | 球種 | gRPC Port | API Port | 服務名稱 |
|-------|------|-----------|----------|----------|
| 1 | 足球 | 40001 | 41001 | GS.Acqu.Worker.1 |
| 3 | 籃球 | 40003 | 41003 | GS.Acqu.Worker.3 |
| 16 | WNBA | 40016 | 41016 | GS.Acqu.Worker.16 |

---

## 部署多個球種範例

```powershell
# 部署籃球 (CatID=3)
.\deploy.ps1 -TargetServer "10.7.0.101" -CatID 3

# 部署足球 (CatID=1)
.\deploy.ps1 -TargetServer "10.7.0.101" -CatID 1

# 部署 WNBA (CatID=16)
.\deploy.ps1 -TargetServer "10.7.0.101" -CatID 16
```

---

## 服務管理指令

```batch
# 檢查服務狀態
sc.exe query GS.Acqu.Worker.3

# 停止服務
sc.exe stop GS.Acqu.Worker.3

# 啟動服務
sc.exe start GS.Acqu.Worker.3

# 重啟服務
sc.exe stop GS.Acqu.Worker.3 && timeout /t 3 && sc.exe start GS.Acqu.Worker.3

# 刪除服務
sc.exe delete GS.Acqu.Worker.3
```

---

## 健康檢查

```powershell
# 檢查服務是否正常
Invoke-RestMethod -Uri "http://10.7.0.101:41003/health"

# 檢查 Channel 統計
Invoke-RestMethod -Uri "http://10.7.0.101:41003/stats/channels"
```

---

## 記錄檔位置

服務的記錄會輸出到 Windows 事件檢視器：
- 應用程式與服務記錄 → .NET Runtime

或使用 stdout 重導向到檔案（需修改 web.config）

---

## 防火牆規則

```batch
# 開放 Port
netsh advfirewall firewall add rule name="GS ACQU gRPC 3" dir=in action=allow protocol=tcp localport=40003
netsh advfirewall firewall add rule name="GS ACQU API 3" dir=in action=allow protocol=tcp localport=41003

# 檢視規則
netsh advfirewall firewall show rule name="GS ACQU gRPC 3"

# 刪除規則
netsh advfirewall firewall delete rule name="GS ACQU gRPC 3"
```
