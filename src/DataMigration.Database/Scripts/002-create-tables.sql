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
    CREATE TABLE dbo.WorkOrders
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_WorkOrders PRIMARY KEY,
        TechnicianId INT NOT NULL,
        ClientId INT NOT NULL,
        Information NVARCHAR(MAX) NOT NULL,
        [Date] DATE NOT NULL,
        Total DECIMAL(18, 2) NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_WorkOrders_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkOrders_Technicians FOREIGN KEY (TechnicianId) REFERENCES dbo.Technicians(Id),
        CONSTRAINT FK_WorkOrders_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id)
    );

    CREATE INDEX IX_WorkOrders_TechnicianId ON dbo.WorkOrders(TechnicianId);
    CREATE INDEX IX_WorkOrders_ClientId ON dbo.WorkOrders(ClientId);
END;
GO
