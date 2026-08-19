-- ============================================================
-- EduMaster — 001_init.sql
-- الإنشاء الأولي: الأشخاص + حسابات الدخول
-- التشغيل: SSMS على خادم فارغ أو موجود — آمن للتكرار (IF NOT EXISTS)
-- ملاحظة: زرع المستخدم admin يتم من التطبيق عند أول تشغيل (DatabaseSeeder)
-- ============================================================

IF DB_ID('EduMasterDb') IS NULL
BEGIN
    CREATE DATABASE EduMasterDb;
END
GO

USE EduMasterDb;
GO

-- =====================================================
-- جدول الأشخاص: الهوية المدنية لأي فرد (طالب، أستاذ، موظف...)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Persons')
BEGIN
    CREATE TABLE Persons
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Persons PRIMARY KEY,
        FirstName           NVARCHAR(50)      NOT NULL,
        LastName            NVARCHAR(50)      NOT NULL,
        FatherName          NVARCHAR(50)      NULL,
        BirthDate           DATE              NULL,
        Gender              TINYINT           NULL,   -- 1 = ذكر، 2 = أنثى
        Phone               NVARCHAR(20)      NULL,
        Phone2              NVARCHAR(20)      NULL,
        Email               NVARCHAR(100)     NULL,
        Address             NVARCHAR(200)     NULL,
        PhotoPath           NVARCHAR(300)     NULL,
        FullNameNormalized  NVARCHAR(150)     NULL,   -- للبحث: بلا تشكيل/تطبيع حروف
        IsActive            BIT               NOT NULL CONSTRAINT DF_Persons_IsActive DEFAULT (1),

        -- حقول التدقيق القياسية (تتكرر في كل الجداول التشغيلية)
        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_Persons_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,
        IsDeleted           BIT               NOT NULL CONSTRAINT DF_Persons_IsDeleted DEFAULT (0),

        CONSTRAINT CK_Persons_Gender CHECK (Gender IS NULL OR Gender IN (1, 2))
    );
END
GO

-- =====================================================
-- جدول حسابات الدخول: من يحق له فتح التطبيق
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserAccounts')
BEGIN
    CREATE TABLE UserAccounts
    (
        Id       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserAccounts PRIMARY KEY,
        PersonId            INT               NOT NULL CONSTRAINT UQ_UserAccounts_PersonId UNIQUE,
        Username            NVARCHAR(50)      NOT NULL CONSTRAINT UQ_UserAccounts_Username UNIQUE,
        PasswordHash        NVARCHAR(500)     NOT NULL,  -- بصمة BCrypt ($2a$11$...)
        IsActive            BIT               NOT NULL CONSTRAINT DF_UserAccounts_IsActive DEFAULT (1),
        FailedLoginCount    INT               NOT NULL CONSTRAINT DF_UserAccounts_FailedCount DEFAULT (0),
        LastLoginAtUtc      DATETIME2         NULL,
        MustChangePassword  BIT               NOT NULL CONSTRAINT DF_UserAccounts_MustChange DEFAULT (0),

        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_UserAccounts_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,
        IsDeleted           BIT               NOT NULL CONSTRAINT DF_UserAccounts_IsDeleted DEFAULT (0),

        CONSTRAINT FK_UserAccounts_Persons FOREIGN KEY (PersonId) REFERENCES Persons(Id)
    );
END
GO

-- تحقق سريع
SELECT name FROM sys.tables ORDER BY name;
GO
