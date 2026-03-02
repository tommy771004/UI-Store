# UI Store 企業級電商平台

## 快速開始

### 1. 設定資料庫
修改 `appsettings.json` 中的 PostgreSQL 密碼：
```json
"DefaultConnection": "Host=localhost;Database=UIStoreDB;Username=postgres;Password=YOUR_PASSWORD;..."
```

或改用 SQLite：
```json
"DefaultConnection": "Data Source=UIStore.db"
```
並將套件改為 `Microsoft.EntityFrameworkCore.Sqlite`

### 2. 執行遷移
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. 啟動應用程式
```bash
dotnet run
```
開啟: https://localhost:5001

## 預設管理員帳號
- Email: admin@uistore.com
- 密碼: 請查看 `ADMIN_CREDENTIALS.txt`

⚠️ **首次登入後請立即修改密碼！**

## 設定第三方服務
請將 `appsettings.json` 中所有 `CHANGE_ME` 更改為實際值。

## 完整功能清單

### Controllers (9個)
✅ HomeController - 首頁、產品詳情、評價  
✅ AccountController - 登入、註冊  
✅ CartController - 購物車管理  
✅ CheckoutController - 結帳、ECPay、LINE Pay  
✅ DownloadsController - 我的購買、訂單查詢  
✅ WishlistController - 願望清單  
✅ PartnersController - 產品上傳、銷售統計  
✅ AdminController - 產品審核、分類管理  
✅ ProductsApiController - RESTful API  

### Views (20+個)
✅ 所有視圖已完整實作  
✅ 響應式設計  
✅ 無任何遺漏  

## 技術棧
- ASP.NET Core 8.0
- Entity Framework Core 8.0
- PostgreSQL / SQLite
- ASP.NET Core Identity
- Swagger/OpenAPI

## 授權
MIT License
