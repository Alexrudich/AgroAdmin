# Миграции Entity Framework Core

## Добавление новой миграции

```powershell
# В Package Manager Console (Visual Studio)
# Default project: AgroAdmin.Infrastructure

Add-Migration InitialMigration -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin -OutputDir Persistence/Migrations
```

## Применение миграции к БД

```powershell
Update-Database -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin
```

## Пример

```powershell
# Добавление гостей и отзывов
Add-Migration AddGuestsAndFeedback -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin -OutputDir Persistence/Migrations

# Применение
Update-Database -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin
```

## Важно

- `-Project AgroAdmin.Infrastructure` - проект с DbContext
- `-StartupProject AgroAdmin` - проект, который запускается (содержит строку подключения в appsettings.json)
- `-OutputDir Persistence/Migrations` - папка для хранения миграций

---

# 🚀 Деплой на MonsterASP.net

## Настройка переменных окружения (Environment Variables)

В панели управления MonsterASP.net необходимо добавить следующие переменные:

| Name | Value | Описание |
|------|-------|----------|
| `ConnectionStrings__DefaultConnection` | `Server=dbXXXX.public.databaseasp.net;Database=dbXXXX;User Id=dbXXXX;Password=XXXX;TrustServerCertificate=True;MultipleActiveResultSets=true` | Строка подключения к БД |
| `Telegram__BotToken` | `ваш_токен` | Токен Telegram бота |
| `Telegram__ChatId` | `ID1,ID2` | ID чатов через запятую |

**Важно:** Используйте двойное подчеркивание `__` для вложенных настроек (например, `Telegram__BotToken` вместо `Telegram:BotToken`).

## Частые проблемы и решения

### 1. Ошибка 500 при запросе к API
- Проверьте логи в панели управления
- Убедитесь, что все переменные окружения заданы правильно
- Проверьте что `Telegram__BotToken` существует и корректен

### 2. Telegram уведомления не приходят
- Убедитесь, что бот добавлен в чат и имеет права
- ChatId можно получить через `@getidsbot` в Telegram
- Для нескольких чатов используйте запятую без пробелов: `ID1,ID2`

### 3. База данных не создается
- Проверьте права пользователя БД
- Убедитесь что строка подключения корректна
- В коде используется `context.Database.Migrate()` при старте