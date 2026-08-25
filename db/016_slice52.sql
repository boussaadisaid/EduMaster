/* ============================================================
   016_slice52.sql — F5 / الشريحة 5.2 «الاحتساب والاعتماد»
   كشوف أجور الفترات (PayrollRuns: مسودة ← اعتماد يقفل نهائياً)
   + سطورها (PayrollLines: محسوبة بلقطة سياسة كاملة D-52 / يدوية مكافأة±خصم بسبب إلزامي — D-123/س-8)
   آمن التكرار: كل كتلة محروسة بفحص وجود.
   «لا تداخل بين الفترات المعتمدة» فحص تطبيقي في الـHandler (روح D-27) — لا فهرس يعبّر عن تداخل مجالات.
   التشغيل: بعد 015_slice51.sql
   ============================================================ */
USE EduMasterDb;
GO

/* ---------- 1) كشوف الأجور ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PayrollRuns' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.PayrollRuns
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PayrollRuns PRIMARY KEY,
        PeriodStart       DATE      NOT NULL,
        PeriodEnd         DATE      NOT NULL,
        Status            TINYINT   NOT NULL,                 -- 1 = مسودة · 2 = معتمد
        TotalCentimes     BIGINT    NOT NULL CONSTRAINT DF_PayrollRuns_Total DEFAULT (0),
        CreatedAtUtc      DATETIME2 NOT NULL,
        CreatedByUserId   INT       NULL CONSTRAINT FK_PayrollRuns_CreatedBy REFERENCES dbo.UserAccounts (Id),
        ApprovedAtUtc     DATETIME2 NULL,
        ApprovedByUserId  INT       NULL CONSTRAINT FK_PayrollRuns_ApprovedBy REFERENCES dbo.UserAccounts (Id),
        CONSTRAINT CK_PayrollRuns_Period CHECK (PeriodEnd >= PeriodStart),
        CONSTRAINT CK_PayrollRuns_Status CHECK (Status IN (1, 2))
    );
END
GO

/* ---------- 2) سطور الكشف ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PayrollLines' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.PayrollLines
    (
        Id                       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PayrollLines PRIMARY KEY,
        RunId                    INT      NOT NULL CONSTRAINT FK_PayrollLines_Run REFERENCES dbo.PayrollRuns (Id) ON DELETE CASCADE,  -- حذف المسودة يحمل سطورها
        PayeeKind                TINYINT  NOT NULL,             -- 1 = أستاذ · 2 = موظف
        TeacherId                INT      NULL CONSTRAINT FK_PayrollLines_Teacher REFERENCES dbo.Teachers (Id),
        EmployeeId               INT      NULL CONSTRAINT FK_PayrollLines_Employee REFERENCES dbo.Employees (Id),
        PayeeName                NVARCHAR(200) NOT NULL,        -- لقطة الاسم — يبقى الكشف مقروءاً ولو تغيّر الاسم
        PolicyId                 INT      NULL CONSTRAINT FK_PayrollLines_Policy REFERENCES dbo.PayPolicies (Id),
        Kind                     TINYINT  NULL,                 -- لقطة نوع السياسة (1..5) — NULL لليدوي
        RateCentimes             BIGINT   NULL,
        Percentage               DECIMAL(5,2) NULL,
        CountsUnjustifiedAbsent  BIT      NULL,
        Quantity                 DECIMAL(12,3) NOT NULL CONSTRAINT DF_PayrollLines_Quantity DEFAULT (0),  -- محسوبون/ساعات/أيام
        SourceKind               TINYINT  NOT NULL,             -- 1 = محسوب · 2 = يدوي (مكافأة/خصم)
        Details                  NVARCHAR(300) NOT NULL,        -- تفصيل مولَّد للمحسوب · سبب إلزامي لليدوي
        AmountCentimes           BIGINT   NOT NULL,             -- سالب لليدوي (خصم) فقط — الحارس في الكيان
        CreatedAtUtc             DATETIME2 NOT NULL,
        CreatedByUserId          INT      NULL CONSTRAINT FK_PayrollLines_CreatedBy REFERENCES dbo.UserAccounts (Id),
        CONSTRAINT CK_PayrollLines_OnePayee CHECK
        (
            (PayeeKind = 1 AND TeacherId IS NOT NULL AND EmployeeId IS NULL)
            OR (PayeeKind = 2 AND EmployeeId IS NOT NULL AND TeacherId IS NULL)
        ),
        CONSTRAINT CK_PayrollLines_SourceKind CHECK (SourceKind IN (1, 2)),
        CONSTRAINT CK_PayrollLines_ManualHasReason CHECK (SourceKind = 1 OR LEN(LTRIM(RTRIM(Details))) > 0),
        CONSTRAINT CK_PayrollLines_ComputedNonNegative CHECK (SourceKind = 2 OR AmountCentimes >= 0)
    );
END
GO

/* ---------- 3) الفهارس ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollLines_RunId' AND object_id = OBJECT_ID(N'dbo.PayrollLines'))
    CREATE NONCLUSTERED INDEX IX_PayrollLines_RunId ON dbo.PayrollLines (RunId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollLines_Payee' AND object_id = OBJECT_ID(N'dbo.PayrollLines'))
    CREATE NONCLUSTERED INDEX IX_PayrollLines_Payee ON dbo.PayrollLines (PayeeKind, TeacherId, EmployeeId) INCLUDE (RunId, AmountCentimes);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PayrollRuns_Status_Period' AND object_id = OBJECT_ID(N'dbo.PayrollRuns'))
    CREATE NONCLUSTERED INDEX IX_PayrollRuns_Status_Period ON dbo.PayrollRuns (Status, PeriodStart, PeriodEnd);
GO