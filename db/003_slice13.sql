-- ============================================================
-- EduMaster — 003_slice13.sql
-- الشريحة 1.3: القفل الزمني للحسابات (تسديد فجوة القفل الدائم)
-- التشغيل: SSMS بعد 002 — آمن للتكرار
-- ملاحظة: لا فهارس بحث — LIKE '%..%' (الاحتواء) لا يستفيد منها،
--         وحجم الجدول (مئات/آلاف) يجعل الفحص الكامل تافه التكلفة محلياً (YAGNI)
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('UserAccounts') AND name = 'LockedUntilUtc')
BEGIN
    ALTER TABLE UserAccounts ADD LockedUntilUtc DATETIME2 NULL;
END
GO

-- تحقق سريع
SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('UserAccounts') ORDER BY column_id;
GO