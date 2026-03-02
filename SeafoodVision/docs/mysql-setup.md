# MySQL Setup Guide for SeafoodVision

This guide explains how to configure and initialise MySQL as the backing database for SeafoodVision.

---

## Prerequisites

- MySQL 8.0 or later installed and running
- .NET 8 SDK installed
- SeafoodVision source code

---

## 1. Create the MySQL Database and User

Connect to MySQL as root (or any superuser) and run the following SQL:

```sql
-- Create the database
CREATE DATABASE IF NOT EXISTS SeafoodVisionDb
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

-- Create a dedicated application user (replace <strong_password> with a real password)
CREATE USER IF NOT EXISTS 'seafood_user'@'localhost' IDENTIFIED BY '<strong_password>';

-- Grant only the required privileges
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, DROP, REFERENCES
    ON SeafoodVisionDb.*
    TO 'seafood_user'@'localhost';

FLUSH PRIVILEGES;
```

> **Security:** Replace `your_password_here` with a strong password and never commit it to source control.
> For remote hosts replace `'localhost'` with the client host (e.g. `'192.168.1.50'`).

---

## 2. Create the Schema (Manual SQL)

If you are not using EF Core migrations, you can create the required table manually:

```sql
USE SeafoodVisionDb;

CREATE TABLE IF NOT EXISTS CountingSessions (
    Id          CHAR(36)     NOT NULL DEFAULT (UUID()),
    CameraId    VARCHAR(128) NOT NULL,
    TotalCount  INT          NOT NULL DEFAULT 0,
    StartedAt   DATETIME(6)  NOT NULL,
    EndedAt     DATETIME(6)  NULL,
    PRIMARY KEY (Id),
    INDEX IX_CountingSessions_StartedAt (StartedAt)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;
```

---

## 3. Configure the Connection String

Open `src/SeafoodVision.Presentation/appsettings.json`. The file ships with an empty password:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=SeafoodVisionDb;User=seafood_user;Password=;"
  }
}
```

**Do not commit your real password to source control.** Instead, supply it at runtime using one of the following approaches:

### Option A – Environment variable (recommended for servers / CI)

The application calls `.AddEnvironmentVariables()` in `App.xaml.cs`, so the .NET double-underscore convention works directly:

```bash
# Windows (PowerShell)
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=3306;Database=SeafoodVisionDb;User=seafood_user;Password=your_password_here;"

# Linux / macOS
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=SeafoodVisionDb;User=seafood_user;Password=your_password_here;"
```

### Option B – User secrets (recommended for local development)

```bash
cd src/SeafoodVision.Presentation
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Port=3306;Database=SeafoodVisionDb;User=seafood_user;Password=your_password_here;"
```

| Parameter  | Description                          | Default           |
|------------|--------------------------------------|-------------------|
| `Server`   | MySQL host name or IP address        | `localhost`       |
| `Port`     | MySQL port                           | `3306`            |
| `Database` | Database / schema name               | `SeafoodVisionDb` |
| `User`     | MySQL user account                   | `seafood_user`    |
| `Password` | Password for the MySQL user account  | *(set via env or user-secrets)* |

---

## 4. Apply EF Core Migrations (Recommended)

The project uses [Pomelo.EntityFrameworkCore.MySql](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql) (version 8.0.0) which supports EF Core 8 migrations against MySQL.

### 4a. Install the EF Core CLI tool (once per machine)

```bash
dotnet tool install --global dotnet-ef
```

### 4b. Add an initial migration

```bash
cd SeafoodVision/src/SeafoodVision.Infrastructure
dotnet ef migrations add InitialCreate \
    --startup-project ../SeafoodVision.Presentation/SeafoodVision.Presentation.csproj
```

### 4c. Apply the migration to the database

```bash
dotnet ef database update \
    --startup-project ../SeafoodVision.Presentation/SeafoodVision.Presentation.csproj
```

EF Core reads the connection string from `appsettings.json` in the startup project and creates/updates the `CountingSessions` table automatically.

---

## 5. Verify the Setup

```sql
USE SeafoodVisionDb;
SHOW TABLES;
DESCRIBE CountingSessions;
```

Expected output:

```
+------------------+
| Tables_in_SeafoodVisionDb |
+------------------+
| CountingSessions |
+------------------+

+------------+--------------+------+-----+---------+-------+
| Field      | Type         | Null | Key | Default | Extra |
+------------+--------------+------+-----+---------+-------+
| Id         | char(36)     | NO   | PRI | NULL    |       |
| CameraId   | varchar(128) | NO   |     | NULL    |       |
| TotalCount | int          | NO   |     | NULL    |       |
| StartedAt  | datetime(6)  | NO   | MUL | NULL    |       |
| EndedAt    | datetime(6)  | YES  |     | NULL    |       |
+------------+--------------+------+-----+---------+-------+
```

---

## 6. Troubleshooting

| Symptom | Likely cause | Resolution |
|---------|--------------|------------|
| `Unable to connect to any of the specified MySQL hosts` | Wrong host/port or MySQL not running | Verify MySQL is started; check `Server` and `Port` values |
| `Access denied for user 'seafood_user'@'localhost'` | Wrong password or user does not exist | Re-run the `CREATE USER` / `GRANT` statements from step 1 |
| `Unknown database 'SeafoodVisionDb'` | Database not created | Run the `CREATE DATABASE` statement from step 1 |
| `Table 'CountingSessions' doesn't exist` | Migrations not applied | Run `dotnet ef database update` from step 4c |
