# RUNBOOK — эксплуатация стенда IsLabApp

Схема развертывания: **GitHub Actions → GHCR → self-hosted runner на сервере → Docker Compose → Nginx**.

Внешний доступ только через Nginx по HTTPS (`https://<IP_VM>/`). Приложение слушает `127.0.0.1:5000`, СУБД доступна только внутри сети Compose.

| Параметр | Значение |
|---|---|
| Каталог деплоя | `/home/deployer/deploy/is-stack` |
| Пользователь деплоя | `deployer` (входит в группу `docker`) |
| Образ приложения | `ghcr.io/teplog/is-lab-app` |
| Каталог резервных копий | `/opt/backups/mssql` |
| Скрипт резервного копирования | `/usr/local/bin/islab-backup.sh` |

## Проверка статуса сервисов

```bash
sudo -iu deployer bash -lc "cd /home/deployer/deploy/is-stack && docker compose ps"
systemctl status nginx --no-pager
systemctl status 'actions.runner.*' --no-pager
```

## Просмотр логов

```bash
sudo -iu deployer bash -lc "cd /home/deployer/deploy/is-stack && docker compose logs --tail 100 app"
sudo -iu deployer bash -lc "cd /home/deployer/deploy/is-stack && docker compose logs --tail 100 mssql"
sudo tail -n 50 /var/log/nginx/is-lab.error.log
```

## Проверка доступности

```bash
curl -k https://<IP_VM>/health
curl -k https://<IP_VM>/version
curl -k https://<IP_VM>/db/ping
```

`/health` подтверждает, что приложение отвечает. `/version` показывает развернутую версию. `/db/ping` подтверждает связь с СУБД: при обрыве возвращает HTTP 503 с текстом ошибки.

## Обновление версии

Автоматически: коммит в `main` запускает CI, тот публикует образ в GHCR, после чего CD разворачивает его на сервере.

Вручную, на конкретный тег:

```bash
sudo -u deployer sed -i "s/^APP_TAG=.*/APP_TAG=<новый_тег>/" /home/deployer/deploy/is-stack/.env
sudo -iu deployer bash -lc "cd /home/deployer/deploy/is-stack && docker compose pull app && docker compose up -d app"
curl -k https://<IP_VM>/version
```

Минимальный набор проверок после обновления: `docker compose ps` показывает оба контейнера в состоянии `Up`, `/version` отдает ожидаемый номер, `/health` и `/db/ping` возвращают `ok`.

## Откат на предыдущую версию

```bash
sudo -u deployer sed -i "s/^APP_TAG=.*/APP_TAG=<предыдущий_рабочий_тег>/" /home/deployer/deploy/is-stack/.env
sudo -iu deployer bash -lc "cd /home/deployer/deploy/is-stack && docker compose pull app && docker compose up -d app"
curl -k https://<IP_VM>/version
curl -k https://<IP_VM>/db/ping
```

Откат возможен потому, что каждая сборка публикуется не только под тегом `latest`, но и под тегом с коротким хешем коммита.

## Резервное копирование

```bash
sudo /usr/local/bin/islab-backup.sh
sudo ls -la /opt/backups/mssql
```

Копии складываются в `/opt/backups/mssql` на хосте — каталог смонтирован в контейнер СУБД как `/var/opt/mssql/backup`, поэтому файлы переживают пересоздание контейнера.

**Политика хранения:** хранятся 5 последних копий, остальные удаляются скриптом автоматически. Каталог доступен только владельцу процесса СУБД (`chmod 700`).

## Проверка восстановления

Восстановление выполняется в отдельную базу, чтобы не затронуть рабочую:

```sql
RESTORE DATABASE IsLabDb_RestoreTest FROM DISK = N'/var/opt/mssql/backup/<файл>.bak'
  WITH MOVE 'IsLabDb' TO '/var/opt/mssql/data/IsLabDb_RestoreTest.mdf',
       MOVE 'IsLabDb_log' TO '/var/opt/mssql/data/IsLabDb_RestoreTest_log.ldf', REPLACE;

USE IsLabDb_RestoreTest;
SELECT COUNT(*) FROM Notes;

DROP DATABASE IsLabDb_RestoreTest;
```

Резервная копия считается пригодной только после успешного восстановления и чтения данных — сам факт создания файла этого не подтверждает.

## Что защищать

Файл `/home/deployer/deploy/is-stack/.env` (пароль `sa`), приватные ключи в `/home/deployer/.ssh/`, секреты репозитория в GitHub. `.env` имеет права `600` и не коммитится — в репозитории лежит только `.env.example` без значений.

## Особенности стенда

Хост-машина на архитектуре ARM, поэтому вместо `mcr.microsoft.com/mssql/server` используется `mcr.microsoft.com/azure-sql-edge` (тот же движок SQL Server, есть сборка под arm64), а вместо контейнера `mssql-tools` — клиент `go-sqlcmd`. После перезапуска контейнера СУБД выпускает сертификат, который Go-клиент отвергает, поэтому команды `sqlcmd` выполняются с `GODEBUG=x509negativeserial=1`.
