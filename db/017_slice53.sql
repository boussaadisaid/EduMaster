/* ============================================================
   017_slice53.sql — F5 / الشريحة 5.3 «الصرف والأرصدة»
   جدول الصرف الموحّد للفريقين (D-116/D-125):
   · إيصالات بتسلسل واحد بلا فجوات — ReceiptNo = MAX+1 داخل معاملة الـHandler (مرآة D-105)،
     وليس IDENTITY لئلا تُحرق أرقام عند التراجع (درس D-122) — والفريد يحرسه قاعدةً
   · المبلغ موجب دائماً للصرف العادي · السالب لقيد التصحيح فقط وبملاحظة إلزامية
     (قداسة الإيصال: لا تعديل ولا حذف أبداً — الخطأ يُقابل بقيد عكسي، روح D-109)
   · PayrollRunId مرجع اختياري معلوماتي («ضمن كشف…») — الصرف على الرصيد الجاري:
     البقية = Σ المعتمد − Σ المصروف عبر التاريخ (الترحيل تلقائي)
   آمن التكرار: محروس بفحص وجود. التشغيل: بعد 016_slice52.sql
   ============================================================ */
USE EduMasterDb;
GO

/* ---------- جدول إيصالات الصرف ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Payouts' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Payouts
    (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payouts PRIMARY KEY,
        ReceiptNo        INT      NOT NULL CONSTRAINT UQ_Payouts_ReceiptNo UNIQUE,  -- التسلسل الموحّد بلا فجوات (مرآة D-105)
        PayeeKind        TINYINT  NOT NULL,                 -- 1 = أستاذ · 2 = موظف
        TeacherId        INT      NULL CONSTRAINT FK_Payouts_Teacher REFERENCES dbo.Teachers (Id),
        EmployeeId       INT      NULL CONSTRAINT FK_Payouts_Employee REFERENCES dbo.Employees (Id),
        PayrollRunId     INT      NULL CONSTRAINT FK_Payouts_Run REFERENCES dbo.PayrollRuns (Id),   -- معلوماتي فقط
        AmountCentimes   BIGINT   NOT NULL,                 -- موجب = صرف · سالب = قيد تصحيح
        Note             NVARCHAR(200) NULL,
        CreatedAtUtc     DATETIME2 NOT NULL,
        CreatedByUserId  INT      NULL CONSTRAINT FK_Payouts_CreatedBy REFERENCES dbo.UserAccounts (Id),
        CONSTRAINT CK_Payouts_OnePayee CHECK
        (
            (PayeeKind = 1 AND TeacherId IS NOT NULL AND EmployeeId IS NULL)
            OR (PayeeKind = 2 AND EmployeeId IS NOT NULL AND TeacherId IS NULL)
        ),
        CONSTRAINT CK_Payouts_NonZero CHECK (AmountCentimes <> 0),
        CONSTRAINT CK_Payouts_CorrectionHasNote CHECK (AmountCentimes > 0 OR LEN(LTRIM(RTRIM(ISNULL(Note, N'')))) > 0)
    );
END
GO

/* ---------- فهرس تجميع الأرصدة ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payouts_Payee' AND object_id = OBJECT_ID(N'dbo.Payouts'))
    CREATE NONCLUSTERED INDEX IX_Payouts_Payee ON dbo.Payouts (PayeeKind, TeacherId, EmployeeId) INCLUDE (AmountCentimes);
GO