# Technician Companion

Technician Companion imports operational work orders from Excel into SQL Server.
It also imports the client list and the technicians found in the work-order
spreadsheet, extracts the date and client from each work order's **Notes**
field, and produces an auditable CSV report for every source row.

The raw Notes value is retained unchanged in `WorkOrders.Information`.

## What the importer does

Given a client workbook and a work-order workbook, the command-line application:

1. inserts any new clients from the `Client` column;
2. inserts any new technicians from the `Technician` column of the work-order
   file;
3. parses each work order's `Notes` column for a date and a client-name
   candidate;
4. matches the candidate against imported clients, accommodating case,
   punctuation, Albanian diacritics, and small spelling differences;
5. validates the technician, client, date, and total; and
6. bulk-inserts only valid work orders and creates a row-by-row CSV report.

The work-order workbook must contain the following headers on its first sheet:

| Column | Purpose |
| --- | --- |
| `Technician` | Technician full name |
| `Notes` | Raw work-order note; used to extract date and client |
| `Total` | Work-order total |

The client workbook must contain exactly one `Client` header with full names.
The work-order workbook must contain all three headers shown above; additional
columns are allowed. Header matching is exact and case-sensitive. The command
rejects paths that do not end in `.xlsx` before reading either workbook.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), for the
  included local SQL Server setup

## First-time setup

Clone the repository, then work from its root directory. The following commands
use a macOS/Linux shell:

```sh
git clone <repository-url>
cd TechnicianCompanion
cp .env.example .env
```

On Windows PowerShell, use:

```powershell
git clone <repository-url>
cd TechnicianCompanion
Copy-Item .env.example .env
```

Set `MSSQL_SA_PASSWORD` in `.env` to a strong SQL Server password. The local
`.env` file is ignored by Git; `.env.example` is the safe template committed to
the repository.

Start SQL Server and wait for its health check to pass:

```sh
docker compose up -d
docker compose ps
```

Create the database and tables. These scripts are idempotent, so they can be
run again safely.

```sh
docker compose exec -T sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -i /scripts/001-create-database.sql'
docker compose exec -T sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -i /scripts/002-create-tables.sql'
```

Restore dependencies and run the test suite:

```sh
dotnet restore
dotnet test
```

## Run an import

Pass absolute paths, or paths relative to the repository root, and use the
password chosen in `.env` in the connection string.

```sh
dotnet run --project src/DataMigration.Cli -- \
  --clients-file "/path/to/Finance - Clients Data.xlsx" \
  --work-orders-file "/path/to/Operations - Work Orders Data.xlsx" \
  --connection-string "Server=localhost,1433;Database=TechnicianCompanion;User Id=sa;Password=<your-password>;Encrypt=False;TrustServerCertificate=True"
```

The application prints import progress and a completion summary. It writes
`work-order-import-report.csv` alongside the supplied work-order workbook.
The report has one line per Excel row, ordered by the spreadsheet row number:

| Column | Description |
| --- | --- |
| `Row Index` | Original Excel row number |
| `Successful` | `true` if the work order was inserted; otherwise `false` |
| `Errors` | One or more validation messages, separated by a newline |
| `Technician` | Source technician value |
| `Client` | Extracted or resolved client value |
| `Total` | Source total value |
| `Information` | Original raw Notes value |

An invalid row is reported but is not inserted. Its errors can include an
unmatched technician or client, a missing/invalid date, and a missing/invalid
total. Re-running an import does not add duplicate client or technician rows,
but it **does** insert another copy of each valid work order; deduplicate source
files before rerunning if that is not desired.

## Inspect and stop the local database

The database is exposed on `localhost:1433` and is persisted in Docker's
`sqlserver-data` volume. Stop it while preserving data:

```sh
docker compose down
```

To start it later, run `docker compose up -d` again. To delete the database and
all local SQL Server data, use `docker compose down -v`; this is destructive.

## Architecture

The solution keeps responsibilities deliberately separate:

```text
DataMigration.Cli
       |
       v
DataMigration.Application  -- use case, contracts, import DTOs, CSV reporting
       |             |
       v             v
DataMigration.Extraction   DataMigration.Matching
 Excel/OpenXML reader      Notes parser, date extraction, fuzzy client matching
       \             /
        v           v
      DataMigration.Core -- domain entities and shared string/name utilities
               |
               v
      DataMigration.Database -- SQL Server repositories and schema scripts
```

- **Core** contains only domain entities (`Client`, `Technician`, and
  `WorkOrder`) plus framework-free string and name utilities shared by the use
  case and matching implementation.
- **Extraction** streams worksheet rows with Open XML rather than automating
  Excel.
- **Matching** normalizes names (including diacritics), applies fuzzy matching,
  and only accepts a match when it clears both a confidence threshold and a
  lead over the next best candidate. It can also score name-sized word windows
  in a full note.
- **Application** coordinates the import, performs field validation, and makes
  the report. It owns the use-case models (`MigrationRequest`, results, parsed
  notes, and typed import errors) and the ports it needs (`IExcelReader`,
  repositories, matcher, parser, and report writer). It is intentionally
  independent of SQL Server and Excel details.
- **Database** uses staging temp tables and `SqlBulkCopy` for client,
  technician, and work-order writes. `CreatedAtUtc` is populated by SQL Server
  using `SYSUTCDATETIME()` for auditing purposes.

## Performance and larger imports

Workbook rows are read asynchronously. Work orders and report rows are
processed in batches of 10,000: each batch is bulk-inserted and appended to the
CSV report before its memory is released. Clients and technicians are
deduplicated in memory before their staged upserts; this is appropriate because
they are expected to be much smaller reference-data sets than the work orders.

Each work-order workbook is identified by a SHA-256 hash and mapped to an
`ImportRuns` record. A batch stores its work orders and row-level import results
in one SQL transaction. If it fails, SQL rolls back the whole batch; the
importer records the batch failure in the CSV and continues with later batches.
Re-running the same workbook reuses its import run and its `(ImportRunId, Excel
row index)` unique keys, so already committed rows are not inserted twice.

Client matching currently ranks every imported client for each work order. This
is suitable for typical operational spreadsheets, but its approximate cost is
`work orders × clients`. For very large client lists or multi-million-row
imports, consider adding a normalized-name index/prefix candidate lookup,
batching work orders instead of keeping all valid rows and report rows in
memory, and recording import-run identifiers to make re-runs idempotent.

### TODO: AI-assisted note extraction

The current note parser is deliberately deterministic: it recognizes supported
date formats, client labels, common Albanian phrasing, and name-like text, then
uses fuzzy matching against the client list. It works well for the current
source data, is fast, and gives predictable results. It should not be treated
as a complete natural-language understanding solution.

For the intended scale of up to two million rows, source notes will likely
contain new wording, abbreviations, typos, languages, and incomplete details
that rule-based extraction cannot reliably cover. A future AI-assisted
extraction stage should return structured client/date candidates with a
confidence score. Low-confidence or conflicting results should remain in the
CSV report for review rather than being silently inserted. To make that viable
at this scale, process requests in batches, cache repeated notes, retain the
raw note and model/version metadata, and keep the existing deterministic path
as a fallback for simple, high-confidence cases.

## Known limitations and operational notes

- Input header names must exactly match the names listed above.
- Dates currently accept little-endian numeric forms with a four-digit year,
  such as `10/12/2024`, `10-12-2024`, or `10.12.2024`. A date such as `4/1/24`
  is reported as invalid.
- Matching is intentionally conservative: close competing client names are
  reported as ambiguous rather than silently choosing one.
- Database repositories use handwritten SQL strings. Runtime values are
  parameterized, so this is not an SQL-injection concern, but schema changes
  require SQL and C# repository code to be kept in sync. A migration tool or a
  data-access abstraction could reduce that maintenance risk as the schema
  grows.
- Foreign keys prevent a work order from referring to a missing client or
  technician. The report is the primary place to review rows that were skipped.

## Development

Run all tests:

```sh
dotnet test TechnicianCompanion.sln
```

The matching tests cover diacritic differences such as `Andi Muçobegaj` and
`Andi Mucobega`, names embedded in Albanian sentences, and common note parsing
formats. Add representative notes and client-name variations whenever the input
format evolves.
