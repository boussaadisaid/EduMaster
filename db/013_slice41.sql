-- ============================================================
-- EduMaster — 013_slice41.sql
-- الشريحة 4.1: المستحقات (Charges) + الترحيل الرجعي
-- التشغيل: SSMS بعد 012 — آمن للتكرار (IF NOT EXISTS + NOT EXISTS)
-- القواعد المحسومة:
--   · D-103: توليد ذرّي مع التسجيل السنوي (حقوق) والشراء (حزمة) — يُتخطّى عند 0
--   · D-108: لا حذف — تسوية موثقة (إلغاء بسبب / تخفيض بمبلغ وسبب) · الأصلي محفوظ
--   · فهرسان مفلتران فريدان على المصدر: مستحق واحد لكل تسجيل/مشتراة أبداً
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Charges')
BEGIN
    CREATE TABLE Charges
    (
        Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Charges PRIMARY KEY,
        StudentId              INT               NOT NULL,
        Kind                   TINYINT           NOT NULL,               -- 1=حقوق تسجيل · 2=حزمة حصص
        AnnualEnrollmentId     INT               NULL,
        GroupSessionPurchaseId INT               NULL,
        OriginalAmountCentimes BIGINT            NOT NULL,
        AmountCentimes         BIGINT            NOT NULL,               -- الحالي بعد التخفيض
        Status                 TINYINT           NOT NULL CONSTRAINT DF_Charges_Status DEFAULT (1),  -- 1=فعّال · 2=ملغى
        AdjustmentNote         NVARCHAR(200)     NULL,                   -- سبب الإلغاء/التخفيض (D-108)
        CancelledAtUtc         DATETIME2         NULL,

        CreatedAtUtc           DATETIME2         NOT NULL CONSTRAINT DF_Charges_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId        INT               NULL,
        UpdatedAtUtc           DATETIME2         NULL,
        UpdatedByUserId        INT               NULL,

        CONSTRAINT FK_Charges_Students   FOREIGN KEY (StudentId)              REFERENCES Students(Id),
        CONSTRAINT FK_Charges_Annual     FOREIGN KEY (AnnualEnrollmentId)     REFERENCES AnnualEnrollments(Id),
        CONSTRAINT FK_Charges_Purchases  FOREIGN KEY (GroupSessionPurchaseId) REFERENCES GroupSessionPurchases(Id),
        CONSTRAINT CK_Charges_Kind       CHECK (Kind IN (1, 2)),
        CONSTRAINT CK_Charges_Status     CHECK (Status IN (1, 2)),
        CONSTRAINT CK_Charges_Amounts    CHECK (OriginalAmountCentimes > 0 AND AmountCentimes >= 0 AND AmountCentimes <= OriginalAmountCentimes),
        CONSTRAINT CK_Charges_Source     CHECK (
            (Kind = 1 AND AnnualEnrollmentId IS NOT NULL AND GroupSessionPurchaseId IS NULL) OR
            (Kind = 2 AND GroupSessionPurchaseId IS NOT NULL AND AnnualEnrollmentId IS NULL))
    );
END
GO

-- مستحق واحد لكل مصدر أبداً (يُحكِم التوليد والترحيل معاً)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Charges_AnnualEnrollment' AND object_id = OBJECT_ID('Charges'))
BEGIN
    CREATE UNIQUE INDEX UX_Charges_AnnualEnrollment ON Charges (AnnualEnrollmentId) WHERE AnnualEnrollmentId IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Charges_Purchase' AND object_id = OBJECT_ID('Charges'))
BEGIN
    CREATE UNIQUE INDEX UX_Charges_Purchase ON Charges (GroupSessionPurchaseId) WHERE GroupSessionPurchaseId IS NOT NULL;
END
GO

-- قراءات لوحة الطالب ومجاميع الدين
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Charges_Student' AND object_id = OBJECT_ID('Charges'))
BEGIN
    CREATE INDEX IX_Charges_Student ON Charges (StudentId, Status);
END
GO

-- الترحيل الرجعي (D-103): مستحقات حقوق التسجيل للتسجيلات القائمة (بحقوق > 0 — الإعفاء بلا مستحق)
IF NOT EXISTS (SELECT 1 FROM Charges WHERE Kind = 1)
BEGIN
    INSERT INTO Charges (StudentId, Kind, AnnualEnrollmentId, GroupSessionPurchaseId, OriginalAmountCentimes, AmountCentimes, Status, CreatedAtUtc, CreatedByUserId)
    SELECT ae.StudentId, 1, ae.Id, NULL, ae.AgreedRegistrationFeeCentimes, ae.AgreedRegistrationFeeCentimes, 1, SYSUTCDATETIME(), NULL
    FROM AnnualEnrollments ae
    WHERE ae.AgreedRegistrationFeeCentimes > 0
      AND NOT EXISTS (SELECT 1 FROM Charges c WHERE c.AnnualEnrollmentId = ae.Id);
END
GO

-- الترحيل الرجعي: مستحقات حزم الحصص للمشتريات القائمة (قيمة = عدد × سعر مسنابشوت — D-96 · يُتخطّى الصفري)
INSERT INTO Charges (StudentId, Kind, AnnualEnrollmentId, GroupSessionPurchaseId, OriginalAmountCentimes, AmountCentimes, Status, CreatedAtUtc, CreatedByUserId)
SELECT cge.StudentId, 2, NULL, p.Id, p.SessionsCount * cge.AgreedUnitPriceCentimes, p.SessionsCount * cge.AgreedUnitPriceCentimes, 1, SYSUTCDATETIME(), NULL
FROM GroupSessionPurchases p
JOIN ClassGroupEnrollments cge ON cge.Id = p.ClassGroupEnrollmentId
WHERE p.SessionsCount * cge.AgreedUnitPriceCentimes > 0
  AND NOT EXISTS (SELECT 1 FROM Charges c WHERE c.GroupSessionPurchaseId = p.Id);
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name = 'Charges';
SELECT Kind, COUNT(*) AS ChargesCount, SUM(AmountCentimes) AS TotalCentimes FROM Charges GROUP BY Kind;
GO