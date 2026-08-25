/* ============================================================
   015_slice51.sql — F5 / الشريحة 5.1 «الأساس»
   الموظفون (أشخاص بنوع موظف — D-115) + سجل أيام العمل + سياسات الأجر
   الموحّدة (D-113/D-114) + لقطة الأستاذ على الحصة (D-117)
   آمن التكرار: كل كتلة محروسة بفحص وجود.
   D-118: عمود مضاف بـALTER لا يُرى في دفعته — كل استعمال لاحق في دفعة مستقلة بعد GO.
   ============================================================ */
USE EduMasterDb;
GO

/* ---------- 1أ) لقطة الأستاذ: إضافة العمود (D-117) ---------- */
IF COL_LENGTH('dbo.ClassSessions', 'TeacherId') IS NULL
BEGIN
    ALTER TABLE dbo.ClassSessions ADD TeacherId INT NULL;
END
GO

/* ---------- 1ب) مفتاحه الخارجي — دفعة مستقلة ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ClassSessions_Teachers' AND parent_object_id = OBJECT_ID('dbo.ClassSessions'))
BEGIN
    ALTER TABLE dbo.ClassSessions ADD CONSTRAINT FK_ClassSessions_Teachers
        FOREIGN KEY (TeacherId) REFERENCES dbo.Teachers(Id);
END
GO

/* ---------- 1ج) الترحيل الرجعي — دفعة مستقلة: المُقامة القائمة تُنسب لأستاذ فوجها الحالي ---------- */
UPDATE cs
SET cs.TeacherId = cg.TeacherId
FROM dbo.ClassSessions cs
JOIN dbo.ClassGroups cg ON cg.Id = cs.ClassGroupId
WHERE cs.Status = 2 AND cs.TeacherId IS NULL AND cg.TeacherId IS NOT NULL;
GO

/* ---------- 1د) فهرس الاحتساب — دفعة مستقلة ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassSessions_TeacherId_Status_StartsAt' AND object_id = OBJECT_ID('dbo.ClassSessions'))
BEGIN
    CREATE INDEX IX_ClassSessions_TeacherId_Status_StartsAt
        ON dbo.ClassSessions (TeacherId, Status, StartsAt);
END
GO

/* ---------- 2) الموظفون (D-115) — مرآة Teachers فوق Persons ---------- */
IF OBJECT_ID('dbo.Employees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
        PersonId INT NOT NULL CONSTRAINT FK_Employees_Persons REFERENCES dbo.Persons(Id),
        JobTitle NVARCHAR(100) NOT NULL,
        Notes NVARCHAR(500) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Employees_IsDeleted DEFAULT (0),
        CreatedAtUtc DATETIME2 NOT NULL,
        CreatedByUserId INT NULL CONSTRAINT FK_Employees_CreatedBy REFERENCES dbo.UserAccounts(Id),
        UpdatedAtUtc DATETIME2 NULL,
        UpdatedByUserId INT NULL CONSTRAINT FK_Employees_UpdatedBy REFERENCES dbo.UserAccounts(Id)
    );

    /* نمط D-39: ملف فعّال واحد لكل شخص — الحذف منطقي ويتيح إعادة الإنشاء */
    CREATE UNIQUE INDEX UX_Employees_PersonId_Active
        ON dbo.Employees (PersonId) WHERE IsDeleted = 0;
END
GO

/* ---------- 3) سجل أيام العمل (للأجر اليومي غير المنتظم — D-115) ---------- */
IF OBJECT_ID('dbo.EmployeeWorkLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeWorkLog
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeWorkLog PRIMARY KEY,
        EmployeeId INT NOT NULL CONSTRAINT FK_EmployeeWorkLog_Employees REFERENCES dbo.Employees(Id),
        WorkDate DATE NOT NULL,
        Note NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        CreatedByUserId INT NULL CONSTRAINT FK_EmployeeWorkLog_CreatedBy REFERENCES dbo.UserAccounts(Id),

        /* لا يوم مكرر لنفس الموظف */
        CONSTRAINT UX_EmployeeWorkLog_Employee_Date UNIQUE (EmployeeId, WorkDate)
    );
END
GO

/* ---------- 4) سياسات الأجر الموحّدة (D-113/D-114) ---------- */
IF OBJECT_ID('dbo.PayPolicies', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PayPolicies
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PayPolicies PRIMARY KEY,
        PayeeKind TINYINT NOT NULL CONSTRAINT CK_PayPolicies_PayeeKind CHECK (PayeeKind IN (1, 2)), /* 1 أستاذ · 2 موظف */
        TeacherId INT NULL CONSTRAINT FK_PayPolicies_Teachers REFERENCES dbo.Teachers(Id),
        EmployeeId INT NULL CONSTRAINT FK_PayPolicies_Employees REFERENCES dbo.Employees(Id),
        ClassGroupId INT NULL CONSTRAINT FK_PayPolicies_ClassGroups REFERENCES dbo.ClassGroups(Id),
        Kind TINYINT NOT NULL CONSTRAINT CK_PayPolicies_Kind CHECK (Kind IN (1, 2, 3, 4, 5)), /* 1 لكل حاضر · 2 نسبة · 3 بالساعة · 4 باليوم · 5 شهري */
        RateCentimes BIGINT NOT NULL CONSTRAINT DF_PayPolicies_Rate DEFAULT (0),
        Percentage DECIMAL(5, 2) NULL CONSTRAINT CK_PayPolicies_Percentage CHECK (Percentage > 0 AND Percentage <= 100),
        CountsUnjustifiedAbsent BIT NOT NULL CONSTRAINT DF_PayPolicies_Absent DEFAULT (0), /* D-114: الافتراضي لا يُحتسب */
        IsActive BIT NOT NULL CONSTRAINT DF_PayPolicies_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2 NOT NULL,
        CreatedByUserId INT NULL CONSTRAINT FK_PayPolicies_CreatedBy REFERENCES dbo.UserAccounts(Id),
        UpdatedAtUtc DATETIME2 NULL,
        UpdatedByUserId INT NULL CONSTRAINT FK_PayPolicies_UpdatedBy REFERENCES dbo.UserAccounts(Id),

        CONSTRAINT CK_PayPolicies_Payee CHECK
        (
            (PayeeKind = 1 AND TeacherId IS NOT NULL AND EmployeeId IS NULL)
            OR (PayeeKind = 2 AND EmployeeId IS NOT NULL AND TeacherId IS NULL)
        ),
        CONSTRAINT CK_PayPolicies_KindByPayee CHECK
        (
            (PayeeKind = 1 AND Kind IN (1, 2, 3))
            OR (PayeeKind = 2 AND Kind IN (4, 5))
        ),
        CONSTRAINT CK_PayPolicies_GroupOnlyTeacher CHECK (PayeeKind = 1 OR ClassGroupId IS NULL),
        CONSTRAINT CK_PayPolicies_Value CHECK
        (
            (Kind = 2 AND Percentage IS NOT NULL AND RateCentimes = 0)
            OR (Kind IN (1, 3, 4, 5) AND Percentage IS NULL AND RateCentimes > 0)
        )
    );

    /* فعّالة واحدة افتراضية لكل أستاذ · تجاوز فعّال واحد لكل (أستاذ، فوج) · فعّالة واحدة لكل موظف */
    CREATE UNIQUE INDEX UX_PayPolicies_Teacher_Default_Active
        ON dbo.PayPolicies (TeacherId) WHERE IsActive = 1 AND PayeeKind = 1 AND ClassGroupId IS NULL;
    CREATE UNIQUE INDEX UX_PayPolicies_Teacher_Group_Active
        ON dbo.PayPolicies (TeacherId, ClassGroupId) WHERE IsActive = 1 AND PayeeKind = 1 AND ClassGroupId IS NOT NULL;
    CREATE UNIQUE INDEX UX_PayPolicies_Employee_Active
        ON dbo.PayPolicies (EmployeeId) WHERE IsActive = 1 AND PayeeKind = 2;
END
GO