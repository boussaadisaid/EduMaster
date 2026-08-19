-- ============================================================
-- EduMaster — 002_academic_years.sql
-- الشريحة 1.2: جدول السنوات الدراسية — أول CRUD أكاديمي
-- التشغيل: SSMS بعد 001_init.sql — آمن للتكرار (IF NOT EXISTS)
-- القواعد المحسومة: اسم فريد + StartDate < EndDate
--   + سنة حالية واحدة كحد أقصى (فهرس مفلتر)
--   + لا حذف للسنوات (قرار الشريحة — لا عمود IsDeleted)
-- ============================================================

USE EduMasterDb;
GO

-- =====================================================
-- جدول السنوات الدراسية
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AcademicYears')
BEGIN
    CREATE TABLE AcademicYears
    (
        Id      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AcademicYears PRIMARY KEY,
        Name                NVARCHAR(20)      NOT NULL CONSTRAINT UQ_AcademicYears_Name UNIQUE,  -- الصيغة: 2025-2026
        StartDate           DATE              NOT NULL,
        EndDate             DATE              NOT NULL,
        IsCurrent           BIT               NOT NULL CONSTRAINT DF_AcademicYears_IsCurrent DEFAULT (0),
        IsActive            BIT               NOT NULL CONSTRAINT DF_AcademicYears_IsActive  DEFAULT (1),

        -- حقول التدقيق القياسية (تتكرر في كل الجداول التشغيلية)
        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_AcademicYears_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,

        CONSTRAINT CK_AcademicYears_Dates CHECK (StartDate < EndDate)
    );
END
GO

-- =====================================================
-- قاعدة «سنة حالية واحدة كحد أقصى» — ضمان على مستوى القاعدة:
-- فهرس فريد مفلتر يسمح بصف واحد فقط بـ IsCurrent = 1
-- (المقايضة الذرّية بين سنتين تتم داخل معاملة في الـ Handler)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_AcademicYears_IsCurrent' AND object_id = OBJECT_ID('AcademicYears'))
BEGIN
    CREATE UNIQUE INDEX UX_AcademicYears_IsCurrent
        ON AcademicYears (IsCurrent)
        WHERE IsCurrent = 1;
END
GO

-- تحقق سريع
SELECT AcademicYearId, Name, StartDate, EndDate, IsCurrent, IsActive
FROM AcademicYears;
GO