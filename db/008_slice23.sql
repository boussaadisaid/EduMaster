-- ============================================================
-- EduMaster — 008_slice23.sql
-- الشريحة 2.3: التسجيل السنوي (AnnualEnrollment)
-- التشغيل: SSMS بعد 007 — آمن للتكرار
-- القواعد المحسومة:
--   · D-37: المستوى/الشعبة سنويان عبر التسجيل لا الملف
--   · D-52/D-72: الحقوق المتفق عليها + ملاحظة — 0 = إعفاء
--   · D-53: نشط/منسحب فقط · العودة بصف جديد — فرادة النشط بفهرس مفلتر (نمط D-39)
--   · D-71: التسجيل في أي سنة فعّالة · الشعبة تطابق المستوى قاعدةً (FK مركّب — بروح D-28)
--   · D-73: أي تسجيل يمنع إزالة ملف الطالب (تفعيل D-55)
-- ============================================================

USE EduMasterDb;
GO

-- =====================================================
-- جدول التسجيلات السنوية
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AnnualEnrollments')
BEGIN
    CREATE TABLE AnnualEnrollments
    (
        Id                            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AnnualEnrollments PRIMARY KEY,
        StudentId                     INT               NOT NULL,
        AcademicYearId                INT               NOT NULL,
        LevelId                       INT               NOT NULL,
        StreamId                      INT               NULL,
        Status                        TINYINT           NOT NULL CONSTRAINT DF_AnnualEnrollments_Status DEFAULT (1),
        AgreedRegistrationFeeCentimes BIGINT            NOT NULL CONSTRAINT DF_AnnualEnrollments_Fee DEFAULT (0),
        RegistrationFeeNote           NVARCHAR(200)     NULL,
        EnrolledAtUtc                 DATETIME2         NOT NULL,
        WithdrawnAtUtc                DATETIME2         NULL,

        CreatedAtUtc                  DATETIME2         NOT NULL CONSTRAINT DF_AnnualEnrollments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId               INT               NULL,
        UpdatedAtUtc                  DATETIME2         NULL,
        UpdatedByUserId               INT               NULL,

        CONSTRAINT FK_AnnualEnrollments_Students      FOREIGN KEY (StudentId)      REFERENCES Students(Id),
        CONSTRAINT FK_AnnualEnrollments_AcademicYears FOREIGN KEY (AcademicYearId) REFERENCES AcademicYears(Id),
        CONSTRAINT FK_AnnualEnrollments_Levels        FOREIGN KEY (LevelId)        REFERENCES Levels(Id),
        -- تطابق الشعبة مع المستوى قاعدةً (بروح D-28) — StreamId الفارغ يمرّ تلقائياً
        CONSTRAINT FK_AnnualEnrollments_Streams       FOREIGN KEY (LevelId, StreamId) REFERENCES Streams(LevelId, Id),
        CONSTRAINT CK_AnnualEnrollments_Status        CHECK (Status IN (1, 2)),   -- 1 نشط · 2 منسحب (D-53)
        CONSTRAINT CK_AnnualEnrollments_Fee           CHECK (AgreedRegistrationFeeCentimes >= 0)
    );
END
GO

-- فرادة النشط: تسجيل سنوي نشط واحد لكل طالب في السنة — المنسحبة لا تحجب إعادة التسجيل (D-53)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_AnnualEnrollments_Student_Year_Active' AND object_id = OBJECT_ID('AnnualEnrollments'))
BEGIN
    CREATE UNIQUE INDEX UX_AnnualEnrollments_Student_Year_Active
    ON AnnualEnrollments (StudentId, AcademicYearId)
    WHERE Status = 1;
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name = 'AnnualEnrollments';
SELECT name, filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('AnnualEnrollments');
GO