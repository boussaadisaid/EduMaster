-- ============================================================
-- EduMaster — 011_slice32.sql
-- الشريحة 3.2: مشتريات الحصص (GroupSessionPurchases) — الرصيد
-- التشغيل: SSMS بعد 010 — آمن للتكرار
-- القواعد المحسومة:
--   · D-91: append-only مرتبط بتسجيل الفوج · الرصيد = Σمشتريات − Σمخصوم
--   · D-96: كمية فقط بلا مبلغ — ثمن الحزمة من السنابشوت (D-50)
--   · D-97: الحصص المبدئية في معاملة الإلحاق ذرّياً · D-99: شراء على نشط فقط
--   · المخصوم يبدأ مع الحضور (3.3 — جدول SessionAttendance) — عموده في القراءة جاهز
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GroupSessionPurchases')
BEGIN
    CREATE TABLE GroupSessionPurchases
    (
        Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GroupSessionPurchases PRIMARY KEY,
        ClassGroupEnrollmentId INT               NOT NULL,
        SessionsCount          INT               NOT NULL,
        PurchasedAtUtc         DATETIME2         NOT NULL,
        Note                   NVARCHAR(200)     NULL,

        CreatedAtUtc           DATETIME2         NOT NULL CONSTRAINT DF_GroupSessionPurchases_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId        INT               NULL,
        UpdatedAtUtc           DATETIME2         NULL,
        UpdatedByUserId        INT               NULL,

        CONSTRAINT FK_GroupSessionPurchases_Enrollments FOREIGN KEY (ClassGroupEnrollmentId) REFERENCES ClassGroupEnrollments(Id),
        CONSTRAINT CK_GroupSessionPurchases_Count       CHECK (SessionsCount > 0)
    );
END
GO

-- مجموعات الرصيد تُقرأ بالتسجيل
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GroupSessionPurchases_Enrollment' AND object_id = OBJECT_ID('GroupSessionPurchases'))
BEGIN
    CREATE INDEX IX_GroupSessionPurchases_Enrollment ON GroupSessionPurchases (ClassGroupEnrollmentId);
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name = 'GroupSessionPurchases';
GO