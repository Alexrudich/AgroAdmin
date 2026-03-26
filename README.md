# 🌿 AgroAdmin — Система управления усадьбой

Проект на .NET 10 (Blazor WebApp) с Event-Driven архитектурой уведомлений через RabbitMQ и фоновыми сервисами для автоматического бэкапа.

## 🛠️ Быстрый старт (Локальный Docker)

Вы можете запустить весь стек (БД, RabbitMQ, Сайт, Воркер) одной командой.

### 1. Прекондишены

В корне проекта (рядом с docker-compose.yml) создайте файл `.env` (он уже добавлен в `.gitignore`) и заполните его по примеру ниже.

### 2. Переменные окружения для локального Docker (файл .env)

```env
# RabbitMQ для локального контейнера
RABBITMQ_URL=amqp://guest:guest@rabbitmq:5672

# Telegram (общие для всех сред)
TELEGRAM_BOT_TOKEN=токен телеграм бота
TELEGRAM_CHAT_ID=ID1,ID2

# База данных в Docker
CONNECTIONSTRINGS__DEFAULTCONNECTION=Server=db;Database=AgroAdminDb;User Id=sa;Password=YourStrong!Password;TrustServerCertificate=True;MultipleActiveResultSets=true

# Google Drive OAuth (локально)
GOOGLE_OAUTH_CREDENTIALS_PATH=/root/.aspnet/google/oauth-credentials.json
GOOGLE_TOKEN_PATH=/root/.aspnet/google/token.json
GOOGLE_PRIVATE_EMAIL=аккаунт google, на диск которого будем закидывать файлы
ASPNETCORE_ENVIRONMENT=Development
```

### 3. Запуск стека

```bash
docker-compose up --build
```

**Доступы:**
- Сайт: http://localhost:8080
- RabbitMQ Admin: http://localhost:15672 (guest/guest)
- MSSQL: localhost:1433

---

## 💻 Разработка в Visual Studio (F5)

Если запускаете проекты из IDE без Docker, используйте User Secrets:

Правой кнопкой на проект `AgroAdmin.NotificationWorker` → **Manage User Secrets**:

```json
{
  "Telegram": {
    "BotToken": "ваш_токен",
    "ChatId": "ID1,ID2"
  },
  "RabbitMQ": {
    "Url": "amqp://guest:guest@localhost:5672"
  }
}
```

---

## 🏗️ Миграции базы данных (EF Core)

**Проект с DbContext:** `AgroAdmin.Infrastructure`  
**Startup проект:** `AgroAdmin`

### Локальное применение (Docker/Local DB)

| Действие | Команда (Package Manager Console) |
|----------|-----------------------------------|
| **Новая миграция** | `Add-Migration Name -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin -OutputDir Persistence/Migrations` |
| **Применить к БД** | `Update-Database -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin` |

### Применение на Продакшн (MonsterASP.net)

```powershell
Update-Database -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin -Connection "ВАША_СТРОКА_ПОДКЛЮЧЕНИЯ_ИЗ_ПАНЕЛИ_MONSTERASP"
```

---

## 🚀 Деплой на MonsterASP.net

Сайт деплоится автоматически через GitHub Actions при пуше в ветку `develop`.

### 📌 Переменные окружения для продакшена

В панели управления MonsterASP необходимо задать следующие переменные:

| Имя переменной | Значение (пример) |
|----------------|-------------------|
| `ConnectionStrings__DefaultConnection` | `Server=ВАША_СТРОКА_ПОДКЛЮЧЕНИЯ_ИЗ_ПАНЕЛИ_MONSTERASP` |
| `Telegram__ChatId` | `ID1,ID2` |
| `Telegram__BotToken` | `токен_вашего_бота` |
| `RabbitMQ__Url` | `amqps://mggmcflf:данные_для_подключения.lmq.cloudamqp.com/mggmcflf` |
| `GOOGLE_TOKEN_PATH` | `D:\Sites\site57408\wwwroot\secrets\token.json` |
| `GOOGLE_OAUTH_CREDENTIALS_PATH` | `D:\Sites\site57408\wwwroot\secrets\oauth-credentials.json` |
| `GOOGLE_PRIVATE_EMAIL` | `ken1234567@gmail.com` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ApiUrl` | `https://agroadmin.runasp.net` |

---

## ☁️ Настройка автоматического бэкапа в Google Drive

### 📁 Необходимые файлы

На продакшене, помимо переменных окружения, нужно вручную загрузить через FTP два файла в папку `wwwroot/secrets/`:

1. **`oauth-credentials.json`** — файл клиента OAuth 2.0 из Google Cloud Console
2. **`token.json`** — файл с Refresh Token, полученный после первой авторизации

**Пример структуры папок на сервере:**
```
D:\Sites\site57408\wwwroot\
├── secrets\
│   ├── oauth-credentials.json
│   └── token.json
├── backups\
│   └── 2026\
│       └── prod_2026-02.xlsx
└── (остальные файлы сайта)
```

### 🔑 Как получить token.json

Для получения токена используется отдельная консольная утилита (см. проект `GoogleTokenHelper` ). После однократной авторизации файл сохраняется локально и затем загружается на сервер.

### 💡 Важно
- **Refresh Token** живёт бесконечно, пока пользователь не отзовёт доступ
- Названия файлов бэкапа автоматически получают префикс среды:
  - `dev_2026-02.xlsx` — для локальной разработки
  - `prod_2026-02.xlsx` — для продакшена

---

## 🐰 Архитектура уведомлений

- `AgroAdmin` (API) отправляет событие `BookingCreatedEvent` в RabbitMQ
- `NotificationWorker` (интегрирован в процесс сайта на проде) ловит событие
- `DatabaseScannerService` сканирует БД и отправляет отложенные напоминания
- `DatabaseBackupService` еженедельно создаёт бэкап и загружает в Google Drive
- `TelegramService` отправляет уведомления админам