-- ============================================================
-- EduMaster — 007_slice22.sql
-- الشريحة 2.2: الأسعار السنوية — SubjectPrices + حقوق التسجيل على AcademicYears
-- التشغيل: SSMS بعد 006 — آمن للتكرار
-- القواعد المحسومة:
--   · D-50: SubjectPrices (سنة/مستوى/مادة) = المصدر الوحيد للحقيقة السعرية — يُنسخ ولا يُشار إليه
--   · D-51: الأموال بالسنتيم BIGINT بلا كسور — الدينار في الواجهة فقط
--   · D-65: حذف فيزيائي حر (بلا IsActive) · صفر مسموح (مجاني صريح) · لا إلزام استباقي
--   · D-66: RegistrationFeeCentimes على AcademicYears — افتراضي 0 = بلا حقوق
-- ============================================================

USE EduMasterDb;
GO

-- =====================================================
-- حقوق التسجيل الافتراضية على السنة (D-66)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AcademicYears') AND name = 'RegistrationFeeCentimes')
BEGIN
    ALTER TABLE AcademicYears
    ADD RegistrationFeeCentimes BIGINT NOT NULL CONSTRAINT DF_AcademicYears_RegistrationFee DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AcademicYears_RegistrationFee' AND parent_object_id = OBJECT_ID('AcademicYears'))
BEGIN
    ALTER TABLE AcademicYears
    ADD CONSTRAINT CK_AcademicYears_RegistrationFee CHECK (RegistrationFeeCentimes >= 0);
END
GO

-- =====================================================
-- جدول أسعار المواد — فرادة (السنة، المستوى، المادة)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubjectPrices')
BEGIN
    CREATE TABLE SubjectPrices
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubjectPrices PRIMARY KEY,
        AcademicYearId      INT               NOT NULL,
        LevelId             INT               NOT NULL,
        SubjectId           INT               NOT NULL,
        UnitPriceCentimes   BIGINT            NOT NULL,

        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_SubjectPrices_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,

        CONSTRAINT FK_SubjectPrices_AcademicYears FOREIGN KEY (AcademicYearId) REFERENCES AcademicYears(Id),
        CONSTRAINT FK_SubjectPrices_Levels        FOREIGN KEY (LevelId)        REFERENCES Levels(Id),
        CONSTRAINT FK_SubjectPrices_Subjects      FOREIGN KEY (SubjectId)      REFERENCES Subjects(Id),
        CONSTRAINT UQ_SubjectPrices_Year_Level_Subject UNIQUE (AcademicYearId, LevelId, SubjectId),
        CONSTRAINT CK_SubjectPrices_UnitPrice     CHECK (UnitPriceCentimes >= 0)
    );
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name = 'SubjectPrices';
SELECT COL_LENGTH('AcademicYears', 'RegistrationFeeCentimes') AS RegistrationFeeColumnExists;   -- غير NULL = موجود
GO