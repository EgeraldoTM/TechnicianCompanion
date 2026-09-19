# Local SQL Server database

The repository includes an idempotent schema in `Scripts/`. Every table has a
`CreatedAtUtc` column whose default is `SYSUTCDATETIME()`, so SQL Server fills it
when a row is inserted.

## Start a local instance

1. Install Docker Desktop and copy the example environment file:

   ```sh
   cp .env.example .env
   ```

2. Replace `MSSQL_SA_PASSWORD` in `.env` with a strong password, then start SQL Server:

   ```sh
   docker compose up -d
   ```

3. Wait until the service is healthy, then create the database and tables:

   ```sh
   docker compose exec -T sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -i /scripts/001-create-database.sql'
   docker compose exec -T sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -i /scripts/002-create-tables.sql'
   ```

The scripts may be run again safely. The local connection string is:

```text
Server=localhost,1433;Database=TechnicianCompanion;User Id=sa;Password=<value from .env>;Encrypt=False;TrustServerCertificate=True
```

## Apple Silicon Macs

The Compose file requests `linux/amd64`; Docker Desktop runs the SQL Server image
through emulation on Apple Silicon. This is suitable for local development, but
the official SQL Server container image is only supported on x86-64 Linux hosts.
