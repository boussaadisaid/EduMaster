-- ============================================================
-- EduMaster — 004_slice14.sql
-- الشريحة 1.4: ملفا الطلاب والأساتذة — أدوار 1:1 فوق نواة Persons
-- التشغيل: SSMS بعد 003 — آمن للتكرار (يصلح تلقائياً نسخة أولى نُفّذت بقيد UNIQUE صلب)
-- القواعد المحسومة: ملف فعّال واحد لكل شخص (فهرس مفلتر — ح-7) · ولي الأمر شخص مرتبط (ح-2 أ)
--   · ولي ≠ الطالب نفسه · صنف الطالب بقيد CHECK · تخصص الأستاذ نص وصفي حر (لا يقود منطقاً)
-- ============================================================

USE EduMasterDb;
GO

-- =====================================================
-- جدول ملفات الطلاب
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Students')
BEGIN
    CREATE TABLE Students
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
        PersonId            INT               NOT NULL,
        GuardianPersonId    INT               NULL,      -- ولي الأمر: شخص مرتبط (ح-2 أ) — nullable: قد يكون الطالب كبيراً
        Category            TINYINT           NOT NULL,  -- صنف الطالب: 1 نظامي · 2 مترشح حر · 3 جامعي · 4 تكوين ودورات
        Notes               NVARCHAR(500)     NULL,

        -- حقول التدقيق القياسية
        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_Students_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,
        IsDeleted           BIT               NOT NULL CONSTRAINT DF_Students_IsDeleted DEFAULT (0),

        CONSTRAINT FK_Students_Persons   FOREIGN KEY (PersonId)         REFERENCES Persons(Id),
        CONSTRAINT FK_Students_Guardians FOREIGN KEY (GuardianPersonId) REFERENCES Persons(Id),
        CONSTRAINT CK_Students_GuardianNotSelf CHECK (GuardianPersonId IS NULL OR GuardianPersonId <> PersonId),
        CONSTRAINT CK_Students_Category CHECK (Category IN (1, 2, 3, 4))
    );
END
GO

-- ح-7: «ملف فعّال واحد لكل شخص» — فهرس مفلتر يتجاهل المحذوفين منطقياً فلا يحجبون إعادة الإنشاء
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Students_PersonId' AND object_id = OBJECT_ID('Students'))
    ALTER TABLE Students DROP CONSTRAINT UQ_Students_PersonId;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Students_PersonId_Active' AND object_id = OBJECT_ID('Students'))
    CREATE UNIQUE INDEX UX_Students_PersonId_Active ON Students(PersonId) WHERE IsDeleted = 0;
GO

-- =====================================================
-- جدول ملفات الأساتذة
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Teachers')
BEGIN
    CREATE TABLE Teachers
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Teachers PRIMARY KEY,
        PersonId            INT               NOT NULL,
        Specialty           NVARCHAR(100)     NULL,      -- وصفي حر — لا يقود منطقاً (المواد كيانات في 1.5)
        Notes               NVARCHAR(500)     NULL,

        -- حقول التدقيق القياسية
        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_Teachers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,
        IsDeleted           BIT               NOT NULL CONSTRAINT DF_Teachers_IsDeleted DEFAULT (0),

        CONSTRAINT FK_Teachers_Persons FOREIGN KEY (PersonId) REFERENCES Persons(Id)
    );
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Teachers_PersonId' AND object_id = OBJECT_ID('Teachers'))
    ALTER TABLE Teachers DROP CONSTRAINT UQ_Teachers_PersonId;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Teachers_PersonId_Active' AND object_id = OBJECT_ID('Teachers'))
    CREATE UNIQUE INDEX UX_Teachers_PersonId_Active ON Teachers(PersonId) WHERE IsDeleted = 0;
GO

-- تحقق سريع
SELECT name, filter_definition FROM sys.indexes
WHERE object_id IN (OBJECT_ID('Students'), OBJECT_ID('Teachers')) AND filter_definition IS NOT NULL;
GO