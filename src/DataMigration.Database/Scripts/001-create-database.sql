USE [master];
GO

IF DB_ID(N'TechnicianCompanion') IS NULL
BEGIN
    CREATE DATABASE [TechnicianCompanion];
END;
GO
