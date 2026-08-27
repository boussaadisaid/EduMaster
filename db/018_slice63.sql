-- ============================================================
-- EduMaster — 018_slice63.sql
-- الشريحة 6.3 (رأسها — ط-7/D-130): هوية المدرسة للمطبوعات — جدول صف واحد SchoolInfo
-- التشغيل: SSMS بعد 017 — آمن للتكرار
-- القواعد المحسومة:
--   · ط-7/D-130: الاسم الظاهر على المطبوعات + هاتف + عنوان + لوغو (قناة IImageStore — D-38)
--   · D-131: الافتراضي والسقوط = «EduMaster» (اسم المنتج — «SchoolSys» مُلغى)
--   · صف واحد قسرياً بقيد CK_SchoolInfo_SingleRow — البذر هنا ساكن بلا منطق تطبيق (لا تعارض مع D-18)
--   · لا IDENTITY عمداً: المعرف يُدرج = 1 صراحة — بعيداً كلياً عن quirk إعادة البذر (D-122)
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SchoolInfo')
BEGIN
    CREATE TABLE SchoolInfo
    (
        Id                INT            NOT NULL CONSTRAINT PK_SchoolInfo PRIMARY KEY,
        Name              NVARCHAR(100)  NOT NULL,
        Phone             NVARCHAR(50)   NULL,
        Address           NVARCHAR(200)  NULL,
        LogoPath          NVARCHAR(260)  NULL,

        CreatedAtUtc      DATETIME2      NOT NULL CONSTRAINT DF_SchoolInfo_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId   INT            NULL,
        UpdatedAtUtc      DATETIME2      NULL,
        UpdatedByUserId   INT            NULL,

        CONSTRAINT CK_SchoolInfo_SingleRow CHECK (Id = 1)
    );
END
GO

-- الصف الوحيد بالاسم الافتراضي (D-131) — آمن التكرار
IF NOT EXISTS (SELECT 1 FROM SchoolInfo WHERE Id = 1)
BEGIN
    INSERT INTO SchoolInfo (Id, Name) VALUES (1, N'EduMaster');
END
GO

-- تحقق سريع
SELECT Id, Name, Phone, Address, LogoPath FROM SchoolInfo;
GO