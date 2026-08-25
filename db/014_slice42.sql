-- ============================================================
-- EduMaster — 014_slice42.sql
-- الشريحة 4.2: القبض (Payments) + التخصيص (PaymentAllocations)
-- التشغيل: SSMS بعد 013 — آمن للتكرار
-- القواعد المحسومة:
--   · D-104: القبض لطالب واحد + PaidByPersonId للدافع الفعلي (الولي D-36)
--   · D-105: رقم إيصال متسلسل مطلق بفرادة + PaidOn تاريخ قابل للاختيار (افتراضه اليوم)
--   · D-107: الزائدة غير المخصصة رصيد دائن مرئي — لا استهلاك سحري
--   · D-108: Kind=2 صرف (إيصال استرجاع باتجاه معاكس) يُجهَّز في المخطط الآن وواجهته في 4.3
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE Payments
    (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
        ReceiptNo          INT               NOT NULL,
        StudentId          INT               NOT NULL,
        PaidByPersonId     INT               NULL,
        Kind               TINYINT           NOT NULL,               -- 1=قبض · 2=صرف (4.3)
        AmountCentimes     BIGINT            NOT NULL,
        PaidOn             DATE              NOT NULL,               -- تاريخ عمل قابل للاختيار — ليس تدقيقاً
        Note               NVARCHAR(200)     NULL,

        CreatedAtUtc       DATETIME2         NOT NULL CONSTRAINT DF_Payments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId    INT               NULL,
        UpdatedAtUtc       DATETIME2         NULL,
        UpdatedByUserId    INT               NULL,

        CONSTRAINT FK_Payments_Students FOREIGN KEY (StudentId)      REFERENCES Students(Id),
        CONSTRAINT FK_Payments_Persons  FOREIGN KEY (PaidByPersonId) REFERENCES Persons(Id),
        CONSTRAINT CK_Payments_Kind     CHECK (Kind IN (1, 2)),
        CONSTRAINT CK_Payments_Amount   CHECK (AmountCentimes > 0)
    );
END
GO

-- رقم الإيصال متسلسل مطلق بفرادة (D-105)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Payments_ReceiptNo' AND object_id = OBJECT_ID('Payments'))
BEGIN
    CREATE UNIQUE INDEX UX_Payments_ReceiptNo ON Payments (ReceiptNo);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Student' AND object_id = OBJECT_ID('Payments'))
BEGIN
    CREATE INDEX IX_Payments_Student ON Payments (StudentId, Kind);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentAllocations')
BEGIN
    CREATE TABLE PaymentAllocations
    (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentAllocations PRIMARY KEY,
        PaymentId      INT               NOT NULL,
        ChargeId       INT               NOT NULL,
        AmountCentimes BIGINT            NOT NULL,

        CreatedAtUtc   DATETIME2         NOT NULL CONSTRAINT DF_PaymentAllocations_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT              NULL,
        UpdatedAtUtc   DATETIME2         NULL,
        UpdatedByUserId INT              NULL,

        CONSTRAINT FK_PaymentAllocations_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id),
        CONSTRAINT FK_PaymentAllocations_Charges  FOREIGN KEY (ChargeId)  REFERENCES Charges(Id),
        CONSTRAINT CK_PaymentAllocations_Amount   CHECK (AmountCentimes > 0)
    );
END
GO

-- نفس المستحق مرة واحدة في الدفعة
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PaymentAllocations_Payment_Charge' AND object_id = OBJECT_ID('PaymentAllocations'))
BEGIN
    CREATE UNIQUE INDEX UX_PaymentAllocations_Payment_Charge ON PaymentAllocations (PaymentId, ChargeId);
END
GO

-- مجموعات المخصوص لكل مستحق (المتبقي = الحالي − مخصوص)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentAllocations_Charge' AND object_id = OBJECT_ID('PaymentAllocations'))
BEGIN
    CREATE INDEX IX_PaymentAllocations_Charge ON PaymentAllocations (ChargeId);
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name IN ('Payments', 'PaymentAllocations');
GO