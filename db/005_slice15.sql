-- ============================================================
-- EduMaster — 005_slice15.sql
-- الشريحة 1.5: البنية الأكاديمية — مستويات + شعب + مواد + قاعات
-- التشغيل: SSMS بعد 004 — آمن للتكرار
-- القواعد المحسومة: بيانات سيادية عابرة للسنوات — لا AcademicYearId (ح-1)
--   · مستوى بلا شعب صالح (ابتدائي/متوسط) وشعبة تتبع مستوى (ح-1)
--   · لا ربط مادة↔مستوى الآن — يُؤجَّل لـF2 (ح-2)
--   · سعة القاعة اختيارية، والقاعة اختيارية دائماً في كل مكان (ح-3)
--   · شاشة واحدة في الإعدادات (ح-4) · تعطيل لا حذف — بلا IsDeleted (ح-5)
--   · اسم المستوى نص حر بلا عمود مرحلة (ح-6)
-- ============================================================

USE EduMasterDb;
GO

-- =====================================================
-- جدول المستويات الدراسية
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Levels')
BEGIN
    CREATE TABLE Levels
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Levels PRIMARY KEY,
        Name                NVARCHAR(100)     NOT NULL CONSTRAINT UQ_Levels_Name UNIQUE,
        SortOrder           INT               NOT NULL CONSTRAINT DF_Levels_SortOrder DEFAULT (0),

        IsActive            BIT               NOT NULL CONSTRAINT DF_Levels_IsActive DEFAULT (1),

        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_Levels_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL
    );
END
GO

-- =====================================================
-- جدول الشعب — تتبع مستوى؛ والمستوى بلا شعب صالح
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Streams')
BEGIN
    CREATE TABLE Streams
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Streams PRIMARY KEY,
        LevelId             INT               NOT NULL,
        Name                NVARCHAR(100)     NOT NULL,

        IsActive            BIT               NOT NULL CONSTRAINT DF_Streams_IsActive DEFAULT (1),

        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_Streams_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,

        CONSTRAINT FK_Streams_Levels FOREIGN KEY (LevelId) REFERENCES Levels(Id),
        CONSTRAINT UQ_Streams_Level_Name UNIQUE (LevelId, Name)   -- الاسم فريد داخل المستوى الواحد فقط (لا عموماً)
    );
END
GO

-- =====================================================
-- جدول المواد
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Subjects')
BEGIN
    CREATE TABLE Subjects
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Subjects PRIMARY KEY,
        Name                NVARCHAR(100)     NOT NULL CONSTRAINT UQ_Subjects_Name UNIQUE,

        IsActive            BIT               NOT NULL CONSTRAINT DF_Subjects_IsActive DEFAULT (1),

        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_Subjects_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL
    );
END
GO

-- =====================================================
-- جدول القاعات — اختيارية دائماً: يمكن العمل معها وبدونها
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Rooms')
BEGIN
    CREATE TABLE Rooms
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Rooms PRIMARY KEY,
        Name                NVARCHAR(50)      NOT NULL CONSTRAINT UQ_Rooms_Name UNIQUE,
        Capacity            INT               NULL,      -- اختيارية (ح-3)

        IsActive            BIT               NOT NULL CONSTRAINT DF_Rooms_IsActive DEFAULT (1),

        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_Rooms_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,

        CONSTRAINT CK_Rooms_Capacity CHECK (Capacity IS NULL OR Capacity > 0)
    );
END
GO

-- تحقق سريع
SELECT name FROM sys.tables ORDER BY name;
GO