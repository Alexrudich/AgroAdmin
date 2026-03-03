# Миграции Entity Framework Core

## Добавление новой миграции

```powershell
# В Package Manager Console (Visual Studio)
# Default project: AgroAdmin.Infrastructure

Add-Migration НазваниеМиграции -Project AgroAdmin.Infrastructure -StartupProject AgroAdmin -OutputDir Persistence/Migrations
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