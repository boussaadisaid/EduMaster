-- EduMaster — 019_expenses.sql
-- Feature: المصاريف التشغيلية
-- يعتمد على AcademicYears الموجودة في 002_academic_years.sql
-- آمن للتكرار.

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExpenseCategories')
BEGIN
    CREATE TABLE dbo.ExpenseCategories
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpenseCategories PRIMARY KEY,
        Name            NVARCHAR(50) NOT NULL,
        IsActive        BIT NOT NULL CONSTRAINT DF_ExpenseCategories_IsActive DEFAULT (1),
        CreatedAtUtc    DATETIME2 NOT NULL CONSTRAINT DF_ExpenseCategories_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT NULL,
        UpdatedAtUtc    DATETIME2 NULL,
        UpdatedByUserId INT NULL,
        CONSTRAINT UQ_ExpenseCategories_Name UNIQUE (Name)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Expenses')
BEGIN
    CREATE TABLE dbo.Expenses
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Expenses PRIMARY KEY,
        AcademicYearId    INT NOT NULL,
        ExpenseCategoryId INT NOT NULL,
        ExpenseDate       DATE NOT NULL,
        AmountCentimes    BIGINT NOT NULL,
        Note              NVARCHAR(500) NULL,
        IsDeleted         BIT NOT NULL CONSTRAINT DF_Expenses_IsDeleted DEFAULT (0),
        CreatedAtUtc      DATETIME2 NOT NULL CONSTRAINT DF_Expenses_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId   INT NULL,
        UpdatedAtUtc      DATETIME2 NULL,
        UpdatedByUserId   INT NULL,
        DeletedAtUtc      DATETIME2 NULL,
        DeletedByUserId   INT NULL,
        CONSTRAINT FK_Expenses_AcademicYears FOREIGN KEY (AcademicYearId) REFERENCES dbo.AcademicYears(Id),
        CONSTRAINT FK_Expenses_ExpenseCategories FOREIGN KEY (ExpenseCategoryId) REFERENCES dbo.ExpenseCategories(Id),
        CONSTRAINT CK_Expenses_AmountPositive CHECK (AmountCentimes > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_AcademicYear_Date' AND object_id = OBJECT_ID('dbo.Expenses'))
BEGIN
    CREATE INDEX IX_Expenses_AcademicYear_Date ON dbo.Expenses (AcademicYearId, ExpenseDate DESC, Id DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Expenses_Category' AND object_id = OBJECT_ID('dbo.Expenses'))
BEGIN
    CREATE INDEX IX_Expenses_Category ON dbo.Expenses (ExpenseCategoryId);
END
GO

INSERT INTO dbo.ExpenseCategories (Name, IsActive, CreatedAtUtc)
SELECT v.Name, 1, SYSUTCDATETIME()
FROM (VALUES (N'الكهرباء'), (N'الغاز'), (N'الماء'), (N'الأدوات واللوازم'), (N'الصيانة'), (N'النقل')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ExpenseCategories ec WHERE ec.Name = v.Name);
GO
