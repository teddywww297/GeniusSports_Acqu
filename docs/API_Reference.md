# GS ACQU API 對接文件

> **Base URL**: `http://{host}:5000`  
> **版本**: v1  
> **更新日期**: 2026-01-05

---

## 目錄

1. [對點服務 API (MatcherController)](#1-對點服務-api)
2. [未對點賽事 API (UnmatchedController)](#2-未對點賽事-api)
3. [列舉定義](#3-列舉定義)
4. [資料結構](#4-資料結構)

---

## 1. 對點服務 API

### 1.1 重新載入對點快取

重新從資料庫載入聯賽和球隊的對點資料到記憶體快取。

**請求**

```
POST /api/matcher/refresh
```

**參數**: 無

**回應**

```json
{
  "message": "對點快取已重新載入"
}
```

**範例**

```bash
curl -X POST http://localhost:5000/api/matcher/refresh
```

---

### 1.2 取得快取統計資訊

查詢目前對點快取中的聯賽和球隊數量。

**請求**

```
GET /api/matcher/stats
```

**參數**: 無

**回應**

| 欄位 | 類型 | 說明 |
|------|------|------|
| `leagueCount` | int | 聯賽對點數量 |
| `teamCount` | int | 球隊對點數量 |

```json
{
  "leagueCount": 1250,
  "teamCount": 8500
}
```

**範例**

```bash
curl http://localhost:5000/api/matcher/stats
```

---

## 2. 未對點賽事 API

### 2.1 取得未對點賽事清單

查詢所有未對點成功的賽事，支援按球種篩選。

**請求**

```
GET /api/unmatched
```

**參數**

| 參數 | 類型 | 必填 | 說明 |
|------|------|------|------|
| `sportType` | int | 否 | 球種類型 (見[列舉定義](#31-sporttype-球種類型)) |

**回應**

```json
[
  {
    "sourceMatchId": "gs_match_12345",
    "source": "GS",
    "sportType": 1,
    "sportTypeName": "Soccer",
    
    "leagueName": "Premier League",
    "leagueNameCn": "英超",
    "leagueNameEn": "Premier League",
    "leagueMatched": true,
    "leagueId": 123,
    
    "homeTeamName": "Manchester United",
    "homeTeamNameCn": "曼聯",
    "homeTeamNameEn": "Manchester United",
    "homeTeamMatched": true,
    "homeTeamId": 456,
    
    "awayTeamName": "New Team FC",
    "awayTeamNameCn": null,
    "awayTeamNameEn": "New Team FC",
    "awayTeamMatched": false,
    "awayTeamId": 0,
    
    "scheduleTime": "2026-01-05T20:00:00",
    
    "reason": 3,
    "reasonName": "TeamNotMatched",
    "matchStatus": "League:✓ Home:✓ Away:✗",
    "isPartialMatch": true,
    "retryCount": 5,
    "createdAt": "2026-01-05T10:30:00",
    "updatedAt": "2026-01-05T15:45:00"
  }
]
```

**範例**

```bash
# 查詢所有未對點賽事
curl http://localhost:5000/api/unmatched

# 查詢足球未對點賽事
curl "http://localhost:5000/api/unmatched?sportType=1"

# 查詢籃球未對點賽事
curl "http://localhost:5000/api/unmatched?sportType=2"
```

---

### 2.2 取得單筆未對點賽事

根據來源賽事編號查詢單筆未對點賽事詳情。

**請求**

```
GET /api/unmatched/{sourceMatchId}
```

**路徑參數**

| 參數 | 類型 | 說明 |
|------|------|------|
| `sourceMatchId` | string | 來源賽事編號 |

**回應**

- 成功: `200 OK` + UnmatchedEventDto
- 不存在: `404 Not Found`

**範例**

```bash
curl http://localhost:5000/api/unmatched/gs_match_12345
```

---

### 2.3 取得未對點賽事數量

查詢未對點賽事總數量，支援按球種篩選。

**請求**

```
GET /api/unmatched/count
```

**參數**

| 參數 | 類型 | 必填 | 說明 |
|------|------|------|------|
| `sportType` | int | 否 | 球種類型 |

**回應**

```json
{
  "count": 42
}
```

**範例**

```bash
# 查詢全部數量
curl http://localhost:5000/api/unmatched/count

# 查詢足球未對點數量
curl "http://localhost:5000/api/unmatched/count?sportType=1"
```

---

### 2.4 取得統計資訊

取得未對點賽事的詳細統計分析。

**請求**

```
GET /api/unmatched/statistics
```

**參數**: 無

**回應**

| 欄位 | 類型 | 說明 |
|------|------|------|
| `totalCount` | int | 總數量 |
| `leagueNotMatchedCount` | int | 聯賽未對點數量 |
| `teamNotMatchedCount` | int | 球隊未對點數量 |
| `partialMatchCount` | int | 部分對點數量 |
| `bySportType` | object | 按球種統計 |
| `byReason` | object | 按原因統計 |

```json
{
  "totalCount": 42,
  "leagueNotMatchedCount": 15,
  "teamNotMatchedCount": 27,
  "partialMatchCount": 20,
  "bySportType": {
    "1": 25,
    "2": 10,
    "3": 7
  },
  "byReason": {
    "2": 15,
    "3": 27
  }
}
```

**範例**

```bash
curl http://localhost:5000/api/unmatched/statistics
```

---

### 2.5 移除未對點賽事

手動移除指定的未對點賽事記錄。

**請求**

```
DELETE /api/unmatched/{sourceMatchId}
```

**路徑參數**

| 參數 | 類型 | 說明 |
|------|------|------|
| `sourceMatchId` | string | 來源賽事編號 |

**回應**

- 成功: `204 No Content`

**範例**

```bash
curl -X DELETE http://localhost:5000/api/unmatched/gs_match_12345
```

---

### 2.6 清除過期賽事

清除指定時間之前的過期未對點賽事。

**請求**

```
POST /api/unmatched/cleanup
```

**參數**

| 參數 | 類型 | 必填 | 預設值 | 說明 |
|------|------|------|--------|------|
| `hours` | int | 否 | 24 | 過期時間 (小時) |

**回應**

```json
{
  "message": "已清除 24 小時前的過期賽事",
  "remainingCount": 15
}
```

**範例**

```bash
# 清除 24 小時前的過期賽事 (預設)
curl -X POST http://localhost:5000/api/unmatched/cleanup

# 清除 48 小時前的過期賽事
curl -X POST "http://localhost:5000/api/unmatched/cleanup?hours=48"
```

---

## 3. 列舉定義

### 3.1 SportType (球種類型)

| 值 | 名稱 | 說明 |
|----|------|------|
| 1 | Soccer | 足球 |
| 2 | Basketball | 籃球 |
| 3 | Baseball | 棒球 |
| 4 | IceHockey | 冰球 |
| 5 | AmericanFootball | 美式足球 |
| 6 | Tennis | 網球 |
| 7 | Esports | 電競 |
| 8 | Volleyball | 排球 |
| 9 | TableTennis | 乒乓球 |
| 10 | Badminton | 羽毛球 |

### 3.2 ProcessResult (處理結果/失敗原因)

| 值 | 名稱 | 說明 |
|----|------|------|
| 0 | Success | 成功 |
| 1 | Skipped | 跳過 |
| 2 | LeagueNotMatched | 聯賽未對點 |
| 3 | TeamNotMatched | 球隊未對點 |
| 4 | MatchNotFound | 賽事不存在 |
| 5 | InvalidData | 無效資料 |
| 6 | DatabaseError | 資料庫錯誤 |

---

## 4. 資料結構

### 4.1 UnmatchedEventDto

| 欄位 | 類型 | 說明 |
|------|------|------|
| `sourceMatchId` | string | 來源賽事編號 (Key) |
| `source` | string | 來源站點 (固定為 "GS") |
| `sportType` | int | 球種類型 |
| `sportTypeName` | string | 球種名稱 |
| **聯賽資訊** | | |
| `leagueName` | string | 聯賽原始名稱 |
| `leagueNameCn` | string? | 聯賽中文名 |
| `leagueNameEn` | string? | 聯賽英文名 |
| `leagueMatched` | bool | 聯賽是否已對點 |
| `leagueId` | int | 聯賽對點 ID (0=未對點) |
| **主隊資訊** | | |
| `homeTeamName` | string | 主隊原始名稱 |
| `homeTeamNameCn` | string? | 主隊中文名 |
| `homeTeamNameEn` | string? | 主隊英文名 |
| `homeTeamMatched` | bool | 主隊是否已對點 |
| `homeTeamId` | int | 主隊對點 ID (0=未對點) |
| **客隊資訊** | | |
| `awayTeamName` | string | 客隊原始名稱 |
| `awayTeamNameCn` | string? | 客隊中文名 |
| `awayTeamNameEn` | string? | 客隊英文名 |
| `awayTeamMatched` | bool | 客隊是否已對點 |
| `awayTeamId` | int | 客隊對點 ID (0=未對點) |
| **賽事資訊** | | |
| `scheduleTime` | datetime | 預定開賽時間 |
| **狀態資訊** | | |
| `reason` | int | 失敗原因代碼 |
| `reasonName` | string | 失敗原因名稱 |
| `matchStatus` | string | 對點狀態摘要 (如: "League:✓ Home:✓ Away:✗") |
| `isPartialMatch` | bool | 是否為部分對點 |
| `retryCount` | int | 重試次數 |
| `createdAt` | datetime | 建立時間 |
| `updatedAt` | datetime | 最後更新時間 |

### 4.2 UnmatchedStatistics

| 欄位 | 類型 | 說明 |
|------|------|------|
| `totalCount` | int | 總數量 |
| `leagueNotMatchedCount` | int | 聯賽未對點數量 |
| `teamNotMatchedCount` | int | 球隊未對點數量 |
| `partialMatchCount` | int | 部分對點數量 |
| `bySportType` | Dictionary<int, int> | 按球種統計 |
| `byReason` | Dictionary<int, int> | 按原因統計 |

---

## 5. 錯誤處理

### HTTP 狀態碼

| 狀態碼 | 說明 |
|--------|------|
| 200 | 成功 |
| 204 | 成功 (無內容) |
| 400 | 請求參數錯誤 |
| 404 | 資源不存在 |
| 500 | 伺服器內部錯誤 |

---

## 6. 注意事項

1. **資料持久化**: 未對點資料存放於記憶體，服務重啟後會清空
2. **即時性**: 由於採用共用記憶體方案，API 查詢結果為即時資料
3. **過期清理**: 系統每小時自動清理超過 24 小時的過期資料
4. **Swagger UI**: 開發環境可訪問 `http://localhost:5000/swagger` 查看互動式文件
