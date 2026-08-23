-- ============================================================
-- EduMaster — 012_slice33.sql
-- الشريحة 3.3: الحضور (SessionAttendance) — خصم الرصيد يبدأ
-- التشغيل: SSMS بعد 011 — آمن للتكرار
-- القواعد المحسومة:
--   · D-93: حاضر(1)/غائب(2) يخصمان · مبرر(3) لا يخصم
--   · D-100: حضور على المُقامة فقط · D-101: تصحيح باستبدال ذرّي · D-102: النشطون فقط يُحضَّرون
--   · فرادة (الحصة، التسجيل): صف واحد لكل مسجَّل في الحصة
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SessionAttendance')
BEGIN
    CREATE TABLE SessionAttendance
    (
        Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SessionAttendance PRIMARY KEY,
        ClassSessionId         INT               NOT NULL,
        ClassGroupEnrollmentId INT               NOT NULL,
        Status                 TINYINT           NOT NULL,
        Note                   NVARCHAR(200)     NULL,
        MarkedAtUtc            DATETIME2         NOT NULL,

        CreatedAtUtc           DATETIME2         NOT NULL CONSTRAINT DF_SessionAttendance_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId        INT               NULL,
        UpdatedAtUtc           DATETIME2         NULL,
        UpdatedByUserId        INT               NULL,

        CONSTRAINT FK_SessionAttendance_Sessions    FOREIGN KEY (ClassSessionId)         REFERENCES ClassSessions(Id),
        CONSTRAINT FK_SessionAttendance_Enrollments FOREIGN KEY (ClassGroupEnrollmentId) REFERENCES ClassGroupEnrollments(Id),
        CONSTRAINT CK_SessionAttendance_Status      CHECK (Status IN (1, 2, 3))
    );
END
GO

-- صف واحد لكل مسجَّل في الحصة
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SessionAttendance_Session_Enrollment' AND object_id = OBJECT_ID('SessionAttendance'))
BEGIN
    CREATE UNIQUE INDEX UX_SessionAttendance_Session_Enrollment ON SessionAttendance (ClassSessionId, ClassGroupEnrollmentId);
END
GO

-- مجموعات المخصوم للرصيد تُقرأ بالتسجيل (D-93)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SessionAttendance_Enrollment' AND object_id = OBJECT_ID('SessionAttendance'))
BEGIN
    CREATE INDEX IX_SessionAttendance_Enrollment ON SessionAttendance (ClassGroupEnrollmentId);
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name = 'SessionAttendance';
GO