# GS ACQU 未對點賽事 API 文件

> 版本：1.0  
> 更新日期：2026-01-07  
> 服務名稱：GS ACQU Worker

---

## 基本資訊

| 項目 | 說明 |
|------|------|
| Base URL | `http://{伺服器IP}:41003` |
| 協定 | HTTP/1.1 |
| 格式 | JSON |
| 編碼 | UTF-8 |

### Port 對照表

| CatID | 球種 | API Port |
|-------|------|----------|
| 1 | 足球 | 41001 |
| 3 | 籃球 | 41003 |
| 16 | WNBA | 41016 |

---

## API 端點總覽

| 方法 | 端點 | 說明 |
|------|------|------|
| GET | `/api/Unmatched` | 取得未對點賽事清單 |
| GET | `/api/Unmatched/count` | 取得未對點賽事數量 |
| DELETE | `/api/Unmatched/{sourceMatchId}` | 移除未對點賽事 |
| GET | `/health` | 健康檢查 |

---

## 1. 取得未對點賽事清單

### Request

```http
GET /api/Unmatched?sportType={sportType}
```

### 參數

| 參數名 | 類型 | 必填 | 說明 |
|--------|------|------|------|
| sportType | integer | 否 | 球種類型 (見下方球種代碼表)，不傳則回傳所有球種 |

### 球種代碼 (SportType)

| 代碼 | 名稱 | 說明 |
|------|------|------|
| 1 | Soccer | 足球 |
| 3 | Basketball | 籃球 (含 NBA、CBA 等) |
| 16 | WNBA | 女子籃球 |

> **注意**：`sportType` 對應的是 Genius Sports 的球種代碼，與資料庫 `CatID` 相同。

### 參數使用範例

| 情境 | URL |
|------|-----|
| 查詢所有未對點賽事 | `/api/Unmatched` |
| 只查詢籃球 | `/api/Unmatched?sportType=3` |
| 只查詢足球 | `/api/Unmatched?sportType=1` |

### 範例請求

```bash
# 取得所有未對點賽事
curl -X GET "http://localhost:41003/api/Unmatched"

# 取得籃球未對點賽事
curl -X GET "http://localhost:41003/api/Unmatched?sportType=3"
```

### Response

#### 成功 (200 OK)

```json
[
  {
    "sourceMatchId": "12842228",
    "source": "GS",
    "sportType": 3,
    "sportTypeName": "Basketball",
    "leagueName": "NBA美國職業籃球",
    "leagueNameCn": null,
    "leagueNameEn": null,
    "leagueMatched": true,
    "leagueId": 19522,
    "homeTeamName": "金州勇士",
    "homeTeamNameCn": null,
    "homeTeamNameEn": null,
    "homeTeamMatched": true,
    "homeTeamId": 12345,
    "awayTeamName": "洛杉磯湖人",
    "awayTeamNameCn": null,
    "awayTeamNameEn": null,
    "awayTeamMatched": false,
    "awayTeamId": 0,
    "scheduleTime": "2026-01-07T19:30:00",
    "reason": 3,
    "reasonName": "TeamNotFound",
    "matchStatus": "League:✓ Home:✓ Away:✗",
    "isPartialMatch": true,
    "retryCount": 3,
    "createdAt": "2026-01-07T14:30:00",
    "updatedAt": "2026-01-07T14:35:00"
  }
]
```

### 回應欄位說明

| 欄位 | 類型 | 說明 |
|------|------|------|
| sourceMatchId | string | Genius Sports 來源賽事編號 |
| source | string | 資料來源 (固定為 "GS") |
| sportType | integer | 球種代碼 |
| sportTypeName | string | 球種名稱 |
| leagueName | string | 聯賽名稱 (GS 原始名稱) |
| leagueMatched | boolean | 聯賽是否已對點 |
| leagueId | integer | 對點後的聯賽 ID (0=未對點) |
| homeTeamName | string | 主隊名稱 (GS 原始名稱) |
| homeTeamMatched | boolean | 主隊是否已對點 |
| homeTeamId | integer | 對點後的主隊 ID (0=未對點) |
| awayTeamName | string | 客隊名稱 (GS 原始名稱) |
| awayTeamMatched | boolean | 客隊是否已對點 |
| awayTeamId | integer | 對點後的客隊 ID (0=未對點) |
| scheduleTime | datetime | 預定開賽時間 (ISO 8601) |
| reason | integer | 未對點原因代碼 |
| reasonName | string | 未對點原因名稱 |
| matchStatus | string | 對點狀態摘要 |
| isPartialMatch | boolean | 是否為部分對點 |
| retryCount | integer | 重試次數 |
| createdAt | datetime | 建立時間 |
| updatedAt | datetime | 最後更新時間 |

### 未對點原因 (Reason)

| 代碼 | 名稱 | 說明 |
|------|------|------|
| 2 | LeagueNotFound | 聯賽未對點 |
| 3 | TeamNotFound | 球隊未對點 |
| 4 | DatabaseError | 資料庫錯誤 |

---

## 2. 取得未對點賽事數量

### Request

```http
GET /api/Unmatched/count?sportType={sportType}
```

### 範例請求

```bash
# 取得所有未對點數量
curl -X GET "http://localhost:41003/api/Unmatched/count"

# 取得籃球未對點數量
curl -X GET "http://localhost:41003/api/Unmatched/count?sportType=3"
```

### Response

```json
{
  "count": 15
}
```

---

## 3. 移除未對點賽事

當手動完成對點後，可呼叫此 API 移除該筆記錄。

### Request

```http
DELETE /api/Unmatched/{sourceMatchId}
```

### 參數

| 參數名 | 類型 | 必填 | 說明 |
|--------|------|------|------|
| sourceMatchId | string | 是 | 來源賽事編號 |

### 範例請求

```bash
curl -X DELETE "http://localhost:41003/api/Unmatched/12842228"
```

### Response

| 狀態碼 | 說明 |
|--------|------|
| 204 No Content | 刪除成功 |
| 404 Not Found | 找不到該賽事 |

---

## 4. 健康檢查

### Request

```http
GET /health
```

### 範例請求

```bash
curl -X GET "http://localhost:41003/health"
```

### Response

```json
{
  "status": "Healthy",
  "catID": 3,
  "grpcPort": 40003,
  "apiPort": 41003,
  "timestamp": "2026-01-07T15:30:00.0000000+08:00"
}
```

---

## 5. 其他端點

### 首頁

```http
GET /
```

回應：
```
GS ACQU Service is running. CatID: 3, gRPC: Port 40003, REST API: Port 41003
```

### Channel 統計

```http
GET /stats/channels
```

回應：
```json
{
  "market": {
    "count": 1500,
    "capacity": 50000,
    "dropCount": 0
  },
  "match": {
    "count": 100,
    "capacity": 10000,
    "dropCount": 0
  }
}
```

---

## 使用情境範例

### 情境 1：前端定時輪詢未對點賽事

```javascript
// 每 30 秒取得未對點賽事
setInterval(async () => {
  const response = await fetch('http://10.7.0.xxx:41003/api/Unmatched?sportType=3');
  const data = await response.json();
  
  // 顯示在管理介面
  renderUnmatchedList(data);
}, 30000);
```

### 情境 2：手動對點後移除記錄

```javascript
async function removeUnmatched(sourceMatchId) {
  await fetch(`http://10.7.0.xxx:41003/api/Unmatched/${sourceMatchId}`, {
    method: 'DELETE'
  });
  
  // 重新載入列表
  loadUnmatchedList();
}
```

---

## 錯誤處理

| HTTP 狀態碼 | 說明 |
|-------------|------|
| 200 | 成功 |
| 204 | 刪除成功 (無內容) |
| 400 | 請求參數錯誤 |
| 404 | 找不到資源 |
| 500 | 伺服器內部錯誤 |

---

## 聯絡資訊

如有問題請聯絡開發團隊。
