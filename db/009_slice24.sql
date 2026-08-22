-- ============================================================
-- EduMaster — 009_slice24.sql
-- الشريحة 2.4: تسجيل الفوج (ClassGroupEnrollment) — Snapshot ثلاثي + نقل + حُراس
-- التشغيل: SSMS بعد 008 — آمن للتكرار
-- القواعد المحسومة:
--   · D-03/D-52/D-77: Snapshot ثلاثي داخل الصف (سعر المصدر + الفعلي + ملاحظة الخصم)
--   · D-53: نشط/منسحب فقط · العودة بصف جديد — فرادة النشط بفهرس مفلتر (نمط D-39)
--   · D-54/D-59/D-79: مطابقة التسجيل السنوي + الشعبة ضمن شعب الفوج + السعة صارمة (حُراس Handler)
--   · D-55: تفعيل الحُراس فعلياً (سيادية/أستاذ/فوج/سنوي)
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassGroupEnrollments')
BEGIN
    CREATE TABLE ClassGroupEnrollments
    (
        Id                         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassGroupEnrollments PRIMARY KEY,
        ClassGroupId               INT               NOT NULL,
        StudentId                  INT               NOT NULL,
        AnnualEnrollmentId         INT               NOT NULL,   -- التسجيل السنوي المطابق (D-54)
        Status                     TINYINT           NOT NULL CONSTRAINT DF_ClassGroupEnrollments_Status DEFAULT (1),
        SnapshotUnitPriceCentimes  BIGINT            NOT NULL,   -- سعر المصدر لحظة الإلحاق (D-77)
        AgreedUnitPriceCentimes    BIGINT            NOT NULL,   -- الفعلي بعد الخصم — أساس فوترة F4 (D-52)
        DiscountNote               NVARCHAR(200)     NULL,
        EnrolledAtUtc              DATETIME2         NOT NULL,
        WithdrawnAtUtc             DATETIME2         NULL,

        CreatedAtUtc               DATETIME2         NOT NULL CONSTRAINT DF_ClassGroupEnrollments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId            INT               NULL,
        UpdatedAtUtc               DATETIME2         NULL,
        UpdatedByUserId            INT               NULL,

        CONSTRAINT FK_ClassGroupEnrollments_ClassGroups       FOREIGN KEY (ClassGroupId)       REFERENCES ClassGroups(Id),
        CONSTRAINT FK_ClassGroupEnrollments_Students          FOREIGN KEY (StudentId)          REFERENCES Students(Id),
        CONSTRAINT FK_ClassGroupEnrollments_AnnualEnrollments FOREIGN KEY (AnnualEnrollmentId) REFERENCES AnnualEnrollments(Id),
        CONSTRAINT CK_ClassGroupEnrollments_Status            CHECK (Status IN (1, 2)),   -- 1 نشط · 2 منسحب
        CONSTRAINT CK_ClassGroupEnrollments_SnapshotPrice     CHECK (SnapshotUnitPriceCentimes >= 0),
        CONSTRAINT CK_ClassGroupEnrollments_AgreedPrice       CHECK (AgreedUnitPriceCentimes >= 0)
    );
END
GO

-- فرادة النشط: نشط واحد لكل طالب في الفوج — المنسحبة لا تحجب العودة (D-53)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ClassGroupEnrollments_Group_Student_Active' AND object_id = OBJECT_ID('ClassGroupEnrollments'))
BEGIN
    CREATE UNIQUE INDEX UX_ClassGroupEnrollments_Group_Student_Active
    ON ClassGroupEnrollments (ClassGroupId, StudentId)
    WHERE Status = 1;
END
GO

-- فهرس كاسكيد الانسحاب السنوي وحارس D-54 — النشطة فقط (D-53)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassGroupEnrollments_Annual_Active' AND object_id = OBJECT_ID('ClassGroupEnrollments'))
BEGIN
    CREATE INDEX IX_ClassGroupEnrollments_Annual_Active
    ON ClassGroupEnrollments (AnnualEnrollmentId)
    WHERE Status = 1;
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name = 'ClassGroupEnrollments';
SELECT name, filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('ClassGroupEnrollments');
GO