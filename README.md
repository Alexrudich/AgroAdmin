# 🌿 AgroAdmin — Система управления усадьбой

Проект на .NET 10 (Blazor WebApp) с Event-Driven архитектурой уведомлений через RabbitMQ и Quartz.NET.

## 🛠️ Быстрый старт (Локальный Docker)

Вы можете запустить весь стек (БД, Кролик, Сайт, Воркер) одной командой.

### 1. Прекондишены

В корне проекта (рядом с docker-compose.yml) создайте файл .env (добавьте его в .gitignore) и заполните:
TELEGRAM_BOT_TOKEN=ваш_токен_бота
TELEGRAM_CHAT_ID=ваш_id_чата

### 2. Запуск стека
```bash
bash
docker-compose up --build
```
Сайт: http://localhost:8080
RabbitMQ Admin: http://localhost:15672 (guest/guest)
MSSQL: localhost:1433

### 💻 Разработка в Visual Studio (F5)

Если запускаете проекты из IDE без Docker, используйте User Secrets, чтобы не светить ключи в репозитории.
Настройка User Secrets
Правой кнопкой на проект AgroAdmin.NotificationWorker -> Manage User Secrets:
```json
{
  "Telegram": {
    "BotToken": "ваш_токен",
    "ChatId": "ID1,ID2"
  },
  "RabbitMQ": {
    "Url": "amqp://guest:guest@localhost:5672" 
```

## 🏗️ Миграции базы данных (EF Core)

**Проект с DbContext:** `AgroAdmin.Infrastructure`  
**Startup проект:** `AgroAdmin`

### Локальное применение (Docker/Local DB)

| Действие | Команда (Package Manager Console) |
|----------|-----------------------------------|
| **Новая миграция** | `Add-Migration Name -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin -OutputDir Persistence/Migrations` |
| **Применить к БД** | `Update-Database -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin` |

### Применение на Продакшн (MonsterASP.net)
Так как хостинг не всегда позволяет выполнять миграции автоматически при старте, используйте прямое подключение из Visual Studio:

```powershell
Update-Database -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin -Connection "ВАША_СТРОКА_ПОДКЛЮЧЕНИЯ_ИЗ_ПАНЕЛИ_MONSTERASP"
```
### 🚀 Деплой на MonsterASP.net

Сайт деплоится автоматически через GitHub Actions при пуше в ветку develop.
Переменные окружения (Environment Variables):
Ключ	Описание
ConnectionStrings__DefaultConnection	Строка к внешней MSSQL
Telegram__BotToken	Токен бота
Telegram__ChatId	ID чатов через запятую
RabbitMQ__Url	URL от CloudAMQP (amqps://...)

### 🐰 Архитектура уведомлений

AgroAdmin (API) отправляет событие BookingCreatedEvent в RabbitMQ.
NotificationWorker (интегрирован в процесс сайта на проде) ловит событие.
Quartz.NET планирует задачу ReminderJob.
TelegramService отправляет уведомление админам.
