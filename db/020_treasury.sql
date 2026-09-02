/* ============================================================
   020_treasury.sql — الخزينة والحسابات المالية
   - حسابات خزينة قابلة للإدارة، مع رصيد افتتاحي مستقل.
   - الحركات اليدوية: دخل آخر / مصروف آخر.
   - التحويلات بين الحسابات عملية ذرية واحدة.
   - ربط Payments / Expenses / Payouts بالحساب المالي.
   - ترحيل السجلات القديمة إلى «الخزينة الرئيسية».
   آمن التكرار — بعد 019_expenses.sql
   ============================================================ */
USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name=N'TreasuryAccounts' AND schema_id=SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.TreasuryAccounts
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TreasuryAccounts PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_TreasuryAccounts_IsActive DEFAULT(1),
        OpeningBalanceCentimes BIGINT NOT NULL CONSTRAINT CK_TreasuryAccounts_OpeningBalance CHECK(OpeningBalanceCentimes >= 0),
        CreatedAtUtc DATETIME2 NOT NULL,
        CreatedByUserId INT NULL CONSTRAINT FK_TreasuryAccounts_CreatedBy REFERENCES dbo.UserAccounts(Id),
        UpdatedAtUtc DATETIME2 NULL,
        UpdatedByUserId INT NULL CONSTRAINT FK_TreasuryAccounts_UpdatedBy REFERENCES dbo.UserAccounts(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_TreasuryAccounts_Name' AND object_id=OBJECT_ID(N'dbo.TreasuryAccounts'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_TreasuryAccounts_Name ON dbo.TreasuryAccounts(Name);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.TreasuryAccounts WHERE Name=N'الخزينة الرئيسية')
BEGIN
    INSERT INTO dbo.TreasuryAccounts(Name, IsActive, OpeningBalanceCentimes, CreatedAtUtc)
    VALUES(N'الخزينة الرئيسية', 1, 0, SYSUTCDATETIME());
END
GO

DECLARE @MainTreasuryAccountId INT;
SELECT @MainTreasuryAccountId = Id FROM dbo.TreasuryAccounts WHERE Name=N'الخزينة الرئيسية';

IF COL_LENGTH('dbo.Payments','TreasuryAccountId') IS NULL
    ALTER TABLE dbo.Payments ADD TreasuryAccountId INT NULL;
IF COL_LENGTH('dbo.Expenses','TreasuryAccountId') IS NULL
    ALTER TABLE dbo.Expenses ADD TreasuryAccountId INT NULL;
IF COL_LENGTH('dbo.Payouts','TreasuryAccountId') IS NULL
    ALTER TABLE dbo.Payouts ADD TreasuryAccountId INT NULL;
IF COL_LENGTH('dbo.Payouts','PayoutDate') IS NULL
    ALTER TABLE dbo.Payouts ADD PayoutDate DATE NULL;
GO

DECLARE @MainId INT;
SELECT @MainId = Id FROM dbo.TreasuryAccounts WHERE Name=N'الخزينة الرئيسية';
UPDATE dbo.Payments SET TreasuryAccountId=@MainId WHERE TreasuryAccountId IS NULL;
UPDATE dbo.Expenses SET TreasuryAccountId=@MainId WHERE TreasuryAccountId IS NULL;
UPDATE dbo.Payouts SET TreasuryAccountId=@MainId WHERE TreasuryAccountId IS NULL;
UPDATE dbo.Payouts SET PayoutDate=CONVERT(date, CreatedAtUtc) WHERE PayoutDate IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Payments_TreasuryAccounts')
    ALTER TABLE dbo.Payments ADD CONSTRAINT FK_Payments_TreasuryAccounts FOREIGN KEY(TreasuryAccountId) REFERENCES dbo.TreasuryAccounts(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Expenses_TreasuryAccounts')
    ALTER TABLE dbo.Expenses ADD CONSTRAINT FK_Expenses_TreasuryAccounts FOREIGN KEY(TreasuryAccountId) REFERENCES dbo.TreasuryAccounts(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Payouts_TreasuryAccounts')
    ALTER TABLE dbo.Payouts ADD CONSTRAINT FK_Payouts_TreasuryAccounts FOREIGN KEY(TreasuryAccountId) REFERENCES dbo.TreasuryAccounts(Id);
GO

IF COL_LENGTH('dbo.Payments','TreasuryAccountId') IS NOT NULL
    ALTER TABLE dbo.Payments ALTER COLUMN TreasuryAccountId INT NOT NULL;
IF COL_LENGTH('dbo.Expenses','TreasuryAccountId') IS NOT NULL
    ALTER TABLE dbo.Expenses ALTER COLUMN TreasuryAccountId INT NOT NULL;
IF COL_LENGTH('dbo.Payouts','TreasuryAccountId') IS NOT NULL
    ALTER TABLE dbo.Payouts ALTER COLUMN TreasuryAccountId INT NOT NULL;
IF COL_LENGTH('dbo.Payouts','PayoutDate') IS NOT NULL
    ALTER TABLE dbo.Payouts ALTER COLUMN PayoutDate DATE NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Payments_TreasuryAccount' AND object_id=OBJECT_ID(N'dbo.Payments'))
    CREATE NONCLUSTERED INDEX IX_Payments_TreasuryAccount ON dbo.Payments(TreasuryAccountId, PaidOn);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Expenses_TreasuryAccount' AND object_id=OBJECT_ID(N'dbo.Expenses'))
    CREATE NONCLUSTERED INDEX IX_Expenses_TreasuryAccount ON dbo.Expenses(TreasuryAccountId, ExpenseDate) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Payouts_TreasuryAccount' AND object_id=OBJECT_ID(N'dbo.Payouts'))
    CREATE NONCLUSTERED INDEX IX_Payouts_TreasuryAccount ON dbo.Payouts(TreasuryAccountId, PayoutDate);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name=N'TreasuryTransactions' AND schema_id=SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.TreasuryTransactions
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TreasuryTransactions PRIMARY KEY,
        TreasuryAccountId INT NOT NULL CONSTRAINT FK_TreasuryTransactions_Account REFERENCES dbo.TreasuryAccounts(Id),
        TransactionDate DATE NOT NULL,
        Kind TINYINT NOT NULL CONSTRAINT CK_TreasuryTransactions_Kind CHECK(Kind IN (1,2)),
        AmountCentimes BIGINT NOT NULL CONSTRAINT CK_TreasuryTransactions_Amount CHECK(AmountCentimes > 0),
        Note NVARCHAR(500) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_TreasuryTransactions_IsDeleted DEFAULT(0),
        CreatedAtUtc DATETIME2 NOT NULL,
        CreatedByUserId INT NULL CONSTRAINT FK_TreasuryTransactions_CreatedBy REFERENCES dbo.UserAccounts(Id),
        UpdatedAtUtc DATETIME2 NULL,
        UpdatedByUserId INT NULL CONSTRAINT FK_TreasuryTransactions_UpdatedBy REFERENCES dbo.UserAccounts(Id),
        DeletedAtUtc DATETIME2 NULL,
        DeletedByUserId INT NULL CONSTRAINT FK_TreasuryTransactions_DeletedBy REFERENCES dbo.UserAccounts(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_TreasuryTransactions_AccountDate' AND object_id=OBJECT_ID(N'dbo.TreasuryTransactions'))
    CREATE NONCLUSTERED INDEX IX_TreasuryTransactions_AccountDate ON dbo.TreasuryTransactions(TreasuryAccountId, TransactionDate, Id) WHERE IsDeleted=0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name=N'TreasuryTransfers' AND schema_id=SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.TreasuryTransfers
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TreasuryTransfers PRIMARY KEY,
        FromTreasuryAccountId INT NOT NULL CONSTRAINT FK_TreasuryTransfers_FromAccount REFERENCES dbo.TreasuryAccounts(Id),
        ToTreasuryAccountId INT NOT NULL CONSTRAINT FK_TreasuryTransfers_ToAccount REFERENCES dbo.TreasuryAccounts(Id),
        TransferDate DATE NOT NULL,
        AmountCentimes BIGINT NOT NULL CONSTRAINT CK_TreasuryTransfers_Amount CHECK(AmountCentimes > 0),
        Note NVARCHAR(500) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_TreasuryTransfers_IsDeleted DEFAULT(0),
        CreatedAtUtc DATETIME2 NOT NULL,
        CreatedByUserId INT NULL CONSTRAINT FK_TreasuryTransfers_CreatedBy REFERENCES dbo.UserAccounts(Id),
        DeletedAtUtc DATETIME2 NULL,
        DeletedByUserId INT NULL CONSTRAINT FK_TreasuryTransfers_DeletedBy REFERENCES dbo.UserAccounts(Id),
        CONSTRAINT CK_TreasuryTransfers_DifferentAccounts CHECK(FromTreasuryAccountId <> ToTreasuryAccountId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_TreasuryTransfers_FromAccount' AND object_id=OBJECT_ID(N'dbo.TreasuryTransfers'))
    CREATE NONCLUSTERED INDEX IX_TreasuryTransfers_FromAccount ON dbo.TreasuryTransfers(FromTreasuryAccountId, TransferDate, Id) WHERE IsDeleted=0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_TreasuryTransfers_ToAccount' AND object_id=OBJECT_ID(N'dbo.TreasuryTransfers'))
    CREATE NONCLUSTERED INDEX IX_TreasuryTransfers_ToAccount ON dbo.TreasuryTransfers(ToTreasuryAccountId, TransferDate, Id) WHERE IsDeleted=0;
GO
