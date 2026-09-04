-- ============================================================
-- EduMaster — 022_slice34_session_transfers.sql
-- نقل رصيد الحصص مع نقل الطالب بين الأفواج
-- القاعدة: المشتريات والحضور التاريخيان لا تتغير؛ النقل حركة append-only مستقلة.
-- ============================================================

USE EduMasterDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GroupSessionTransfers')
BEGIN
    CREATE TABLE GroupSessionTransfers
    (
        Id                         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GroupSessionTransfers PRIMARY KEY,
        FromClassGroupEnrollmentId INT               NOT NULL,
        ToClassGroupEnrollmentId   INT               NOT NULL,
        SessionsCount              INT               NOT NULL,
        TransferredAtUtc           DATETIME2         NOT NULL,
        Note                       NVARCHAR(200)     NULL,

        CreatedAtUtc               DATETIME2         NOT NULL CONSTRAINT DF_GroupSessionTransfers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId            INT               NULL,

        CONSTRAINT FK_GroupSessionTransfers_FromEnrollment FOREIGN KEY (FromClassGroupEnrollmentId) REFERENCES ClassGroupEnrollments(Id),
        CONSTRAINT FK_GroupSessionTransfers_ToEnrollment   FOREIGN KEY (ToClassGroupEnrollmentId)   REFERENCES ClassGroupEnrollments(Id),
        CONSTRAINT CK_GroupSessionTransfers_DifferentEnrollments CHECK (FromClassGroupEnrollmentId <> ToClassGroupEnrollmentId),
        CONSTRAINT CK_GroupSessionTransfers_Count CHECK (SessionsCount > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GroupSessionTransfers_FromEnrollment' AND object_id = OBJECT_ID('GroupSessionTransfers'))
BEGIN
    CREATE INDEX IX_GroupSessionTransfers_FromEnrollment ON GroupSessionTransfers (FromClassGroupEnrollmentId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GroupSessionTransfers_ToEnrollment' AND object_id = OBJECT_ID('GroupSessionTransfers'))
BEGIN
    CREATE INDEX IX_GroupSessionTransfers_ToEnrollment ON GroupSessionTransfers (ToClassGroupEnrollmentId);
END
GO

SELECT name FROM sys.tables WHERE name = 'GroupSessionTransfers';
GO
