USE [TechnicianCompanion];
GO

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clients
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Clients PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Clients_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Clients_FirstName_LastName UNIQUE (FirstName, LastName)
    );
END;
GO

IF OBJECT_ID(N'dbo.Technicians', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Technicians
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Technicians PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Technicians_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Technicians_FirstName_LastName UNIQUE (FirstName, LastName)
    );
END;
GO

IF OBJECT_ID(N'dbo.WorkOrders', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'dbo.ImportRuns', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ImportRuns
        (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ImportRuns PRIMARY KEY,
            SourceFileHash CHAR(64) NOT NULL CONSTRAINT UQ_ImportRuns_SourceFileHash UNIQUE,
            SourceFileName NVARCHAR(260) NOT NULL,
            Status NVARCHAR(30) NOT NULL,
            CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_ImportRuns_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CompletedAtUtc DATETIME2(7) NULL
        );
    END;

    CREATE TABLE dbo.WorkOrders
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_WorkOrders PRIMARY KEY,
        TechnicianId INT NOT NULL,
        ClientId INT NOT NULL,
        Information NVARCHAR(MAX) NOT NULL,
        [Date] DATE NOT NULL,
        Total DECIMAL(18, 2) NOT NULL,
        ImportRunId UNIQUEIDENTIFIER NULL,
        SourceRowIndex INT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_WorkOrders_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkOrders_Technicians FOREIGN KEY (TechnicianId) REFERENCES dbo.Technicians(Id),
        CONSTRAINT FK_WorkOrders_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id)
    );

    CREATE INDEX IX_WorkOrders_TechnicianId ON dbo.WorkOrders(TechnicianId);
    CREATE INDEX IX_WorkOrders_ClientId ON dbo.WorkOrders(ClientId);
    CREATE UNIQUE INDEX UX_WorkOrders_ImportRun_SourceRow ON dbo.WorkOrders(ImportRunId, SourceRowIndex) WHERE ImportRunId IS NOT NULL;
END;
GO

IF OBJECT_ID(N'dbo.ImportRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImportRuns
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ImportRuns PRIMARY KEY,
        SourceFileHash CHAR(64) NOT NULL CONSTRAINT UQ_ImportRuns_SourceFileHash UNIQUE,
        SourceFileName NVARCHAR(260) NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_ImportRuns_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CompletedAtUtc DATETIME2(7) NULL
    );
END;
GO

IF COL_LENGTH(N'dbo.WorkOrders', N'ImportRunId') IS NULL
    ALTER TABLE dbo.WorkOrders ADD ImportRunId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.WorkOrders', N'SourceRowIndex') IS NULL
    ALTER TABLE dbo.WorkOrders ADD SourceRowIndex INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_WorkOrders_ImportRun_SourceRow' AND object_id = OBJECT_ID(N'dbo.WorkOrders'))
    CREATE UNIQUE INDEX UX_WorkOrders_ImportRun_SourceRow ON dbo.WorkOrders(ImportRunId, SourceRowIndex) WHERE ImportRunId IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.ImportRowResults', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImportRowResults
    (
        ImportRunId UNIQUEIDENTIFIER NOT NULL,
        SourceRowIndex INT NOT NULL,
        Successful BIT NOT NULL,
        Errors NVARCHAR(MAX) NOT NULL,
        Technician NVARCHAR(200) NOT NULL,
        Client NVARCHAR(200) NOT NULL,
        Total NVARCHAR(100) NOT NULL,
        Information NVARCHAR(MAX) NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_ImportRowResults_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ImportRowResults PRIMARY KEY (ImportRunId, SourceRowIndex),
        CONSTRAINT FK_ImportRowResults_ImportRuns FOREIGN KEY (ImportRunId) REFERENCES dbo.ImportRuns(Id)
    );
END;
GO
