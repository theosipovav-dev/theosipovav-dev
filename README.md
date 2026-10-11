# TFlexDocsControl

Веб-приложение для контроля служб сервера приложений T-FLEX DOCs на Linux (RED OS 8, systemd).

- статус служб (ActiveState/SubState, PID, время работы, память);
- запуск, остановка и перезапуск;
- просмотр журналов (journald) с фильтрами по уровню, периоду и тексту, режим реального времени, выгрузка в файл;
- пользователи с ролями и журнал действий (кто и когда управлял службами);
- данные приложения хранятся во внешней PostgreSQL.

## Состав

| Папка | Что внутри |
|---|---|
| `backend/` | REST API на ASP.NET Core (.NET 8), EF Core + Npgsql, JWT |
| `frontend/` | React 18 + Ant Design 5 (JavaScript, Vite) |
| `deploy/` | unit-файл systemd, правило sudoers, пример production-конфига |

## Роли

| Роль | Просмотр статусов и логов | Start/Stop/Restart | Пользователи и журнал действий |
|---|---|---|---|
| Admin | да | да | да |
| Operator | да | да | нет |
| Viewer | да | нет | нет |

## Настройка служб

Список служб задаётся в секции `ServiceControl:Services` конфигурации backend:

```json
"ServiceControl": {
  "UseSudoForControl": true,
  "UseSudoForJournal": false,
  "Services": [
    {
      "Id": "tflexdocs-server",
      "Unit": "tflexdocs.service",
      "DisplayName": "Сервер приложений T-FLEX DOCs",
      "Description": "Основная служба",
      "AllowControl": true
    }
  ]
}
```

- `Id` используется в адресах API (`/api/services/{id}`), `Unit` - имя systemd unit-а.
- `AllowControl: false` оставляет службу только для просмотра.
- Backend управляет только службами из этого списка; имена unit-ов проверяются при старте.

Узнать имена unit-ов на сервере: `systemctl list-units --type=service --all | grep -i tflex`.

## Права на управление службами

Backend работает от непривилегированного пользователя `tflexctl`:

- **start/stop/restart** выполняются через `sudo -n systemctl ...`; правило в `deploy/sudoers-tflexdocscontrol` разрешает только эти команды и только для перечисленных unit-ов;
- **статус** (`systemctl show`) прав не требует;
- **журналы** читаются через `journalctl`; пользователь должен входить в группу `systemd-journal` (или включите `UseSudoForJournal` и добавьте в sudoers `journalctl`).

## Разработка

Требуется .NET 8 SDK, Node.js 18+ и доступная PostgreSQL.

```bash
# backend: http://localhost:5080, Swagger: http://localhost:5080/swagger
cd backend/TFlexDocsControl.Api
# строка подключения и dev-ключ JWT лежат в appsettings.Development.json
dotnet run

# frontend: http://localhost:5173 (запросы /api проксируются на backend)
cd frontend
npm install
npm run dev
```

При первом запуске backend применяет миграции и создаёт администратора `admin` (в Development пароль `admin12345`, в Production - из `Bootstrap:AdminPassword` либо сгенерированный и выведенный в лог). Смените пароль после входа.

Новая миграция после изменения модели:

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Имя> -p TFlexDocsControl.Api -o Data/Migrations
```

## Развёртывание на RED OS 8

```bash
# 1. .NET 8 runtime
sudo dnf install aspnetcore-runtime-8.0

# 2. Пользователь службы с доступом к журналу
sudo useradd --system --no-create-home --shell /sbin/nologin tflexctl
sudo usermod -aG systemd-journal tflexctl

# 3. Сборка (на машине разработчика)
cd frontend && npm ci && npm run build && cd ..
dotnet publish backend/TFlexDocsControl.Api -c Release -o publish
mkdir -p publish/wwwroot && cp -r frontend/dist/* publish/wwwroot/

# 4. Установка
sudo mkdir -p /opt/tflexdocscontrol /etc/tflexdocscontrol
sudo cp -r publish/* /opt/tflexdocscontrol/
sudo cp deploy/appsettings.production.example.json /etc/tflexdocscontrol/appsettings.json
sudo chown root:tflexctl /etc/tflexdocscontrol/appsettings.json
sudo chmod 640 /etc/tflexdocscontrol/appsettings.json
# отредактируйте строку подключения, Jwt:Key (openssl rand -base64 48) и список служб

# 5. Права на управление службами (поправьте имена unit-ов)
sudo install -m 0440 deploy/sudoers-tflexdocscontrol /etc/sudoers.d/tflexdocscontrol
sudo visudo -cf /etc/sudoers.d/tflexdocscontrol

# 6. Запуск
sudo cp deploy/tflexdocscontrol.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now tflexdocscontrol
journalctl -u tflexdocscontrol -f
```

Backend сам отдаёт собранный фронтенд из `wwwroot`, поэтому приложение открывается по адресу `http://<сервер>:5080`. Для HTTPS поставьте перед ним nginx (заголовки `X-Forwarded-*` поддерживаются).

Конфигурация читается из `appsettings.json` рядом с приложением, затем `/etc/tflexdocscontrol/appsettings.json` (имеет приоритет). Файл в `/etc` не затирается при обновлении.

База данных: создайте в PostgreSQL пустую БД и пользователя с правом создавать таблицы в ней; схему создаст backend при старте.

## API

| Метод | Путь | Роль |
|---|---|---|
| POST | `/api/auth/login` | все |
| GET | `/api/auth/me` | авторизованные |
| POST | `/api/auth/change-password` | авторизованные |
| GET | `/api/services` | авторизованные |
| GET | `/api/services/{id}` | авторизованные |
| POST | `/api/services/{id}/start` \| `stop` \| `restart` | Admin, Operator |
| GET | `/api/services/{id}/logs?lines=&since=&until=&priority=&grep=&afterCursor=` | авторизованные |
| GET/POST | `/api/users` | Admin |
| PUT/DELETE | `/api/users/{id}` | Admin |
| POST | `/api/users/{id}/reset-password` | Admin |
| GET | `/api/audit?page=&pageSize=&username=` | Admin |
| GET | `/api/health` | все |
