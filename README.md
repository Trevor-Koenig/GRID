built for linux docker.

This is an ASP.NET dockerized application and is my personal portfolio and homepage web application.

GRID stands for General Resource & Integration Dashboard.

## Deploying on TrueNAS

TrueNAS's "Install via YAML" has no `.env` file, so keep the compose file and `.env` on a dataset and point the app at them.

1. Create a dataset, e.g. `/mnt/tank/apps/grid`, with the **Generic** preset. Postgres needs to `chmod 700` its data directory, which fails on the SMB/NFSv4 ACL presets.
2. Copy `docker-compose.yml` there, but not `docker-compose.override.yml` (that switches the app into Development mode). Copy `.env.example` to `.env` and set:
   - `PUID=568`, `PGID=568` (the TrueNAS `apps` user)
   - `DATA_DIR=/mnt/tank/apps/grid/data` (an absolute path)
   - `GRID_BIND_ADDRESS=0.0.0.0` if the reverse proxy runs as another app
   - `POSTGRES_PASSWORD` and `MAILGUN_API_KEY`
3. Create the data folders owned by the apps user:
   `mkdir -p data/postgres data/keys data/backups && chown -R 568:568 data`
4. Apps → Discover Apps → ⋮ → Install via YAML:
   ```yaml
   include:
     - /mnt/tank/apps/grid/docker-compose.yml
   ```

## Backups

The admin dashboard has a **Backups** page (permission *Manage Database Backups*). From there you can back up now, download, upload, restore, and delete backups. An automatic backup runs every 24 hours, and the newest 14 are kept. Backups are gzipped SQL dumps stored in `DATA_DIR/backups`.

A restore replaces the whole database in one transaction, so a failed restore changes nothing. The current database is saved as a *pre-restore* backup first. Backups made by older GRID versions are migrated forward after the restore.

**Restoring into a fresh install:** while no accounts exist, the login page links to `/Setup/Restore`. That page asks for the setup code that GRID writes to its container log at startup (TrueNAS: Apps → grid → Logs).


Road map:
1.0.0: app provides a landing page, account creation, information about the developer, and links to existing systems in the domain.
2.0.0: app provides a way to temporarily transfer files between users with accounts
3.0.0: ???
