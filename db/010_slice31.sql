-- ============================================================
-- EduMaster — 010_slice31.sql
-- الشريحة 3.1: جدول استعمال الزمن (ClassGroupSchedules) + الحصص (ClassSessions)
-- التشغيل: SSMS بعد 009 — آمن للتكرار
-- القواعد المحسومة:
--   · D-86: المواعيد قوالب أسبوعية (1=السبت…7=الجمعة) لا تمس المولَّد رجعياً
--   · D-87: فرادة (ClassGroupId, StartsAt) تمنع تكرار التوليد والاستثنائية · SourceScheduleId فارغ = استثنائية
--   · D-90: مجدولة/مُقامة/ملغاة · المُقامة تفتح الحضور والملغاة لا تخصم
--   · StartsAt توقيت عمل محلي (ليس تدقيقاً) — التدقيق يبقى UTC
-- ============================================================

USE EduMasterDb;
GO

-- =====================================================
-- جدول المواعيد الأسبوعية (قوالب التوليد)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassGroupSchedules')
BEGIN
    CREATE TABLE ClassGroupSchedules
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassGroupSchedules PRIMARY KEY,
        ClassGroupId    INT               NOT NULL,
        DayOfWeek       TINYINT           NOT NULL,   -- 1=السبت … 7=الجمعة (D-86)
        StartTime       TIME(0)           NOT NULL,
        DurationMinutes INT               NOT NULL,
        IsActive        BIT               NOT NULL CONSTRAINT DF_ClassGroupSchedules_IsActive DEFAULT (1),

        CreatedAtUtc    DATETIME2         NOT NULL CONSTRAINT DF_ClassGroupSchedules_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT               NULL,
        UpdatedAtUtc    DATETIME2         NULL,
        UpdatedByUserId INT               NULL,

        CONSTRAINT FK_ClassGroupSchedules_ClassGroups FOREIGN KEY (ClassGroupId) REFERENCES ClassGroups(Id),
        CONSTRAINT CK_ClassGroupSchedules_DayOfWeek   CHECK (DayOfWeek BETWEEN 1 AND 7),
        CONSTRAINT CK_ClassGroupSchedules_Duration    CHECK (DurationMinutes > 0)
    );
END
GO

-- =====================================================
-- جدول الحصص
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassSessions')
BEGIN
    CREATE TABLE ClassSessions
    (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassSessions PRIMARY KEY,
        ClassGroupId     INT               NOT NULL,
        SourceScheduleId INT               NULL,   -- فارغ = حصة استثنائية (D-87)
        StartsAt         DATETIME2         NOT NULL,   -- توقيت العمل المحلي (ليس تدقيقاً)
        DurationMinutes  INT               NOT NULL,
        Status           TINYINT           NOT NULL CONSTRAINT DF_ClassSessions_Status DEFAULT (1),   -- 1 مجدولة · 2 مُقامة · 3 ملغاة (D-90)
        Topic            NVARCHAR(200)     NULL,
        CancelledAtUtc   DATETIME2         NULL,

        CreatedAtUtc     DATETIME2         NOT NULL CONSTRAINT DF_ClassSessions_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId  INT               NULL,
        UpdatedAtUtc     DATETIME2         NULL,
        UpdatedByUserId  INT               NULL,

        CONSTRAINT FK_ClassSessions_ClassGroups FOREIGN KEY (ClassGroupId)     REFERENCES ClassGroups(Id),
        CONSTRAINT FK_ClassSessions_Schedules   FOREIGN KEY (SourceScheduleId) REFERENCES ClassGroupSchedules(Id),
        CONSTRAINT CK_ClassSessions_Status      CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_ClassSessions_Duration    CHECK (DurationMinutes > 0)
    );
END
GO

-- فرادة التوقيت للفوج — تمنع تكرار التوليد وازدواج الاستثنائية (D-87)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ClassSessions_Group_StartsAt' AND object_id = OBJECT_ID('ClassSessions'))
BEGIN
    CREATE UNIQUE INDEX UX_ClassSessions_Group_StartsAt ON ClassSessions (ClassGroupId, StartsAt);
END
GO

-- فهرس التصفح الزمني لشاشة الحصص
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassSessions_StartsAt' AND object_id = OBJECT_ID('ClassSessions'))
BEGIN
    CREATE INDEX IX_ClassSessions_StartsAt ON ClassSessions (StartsAt);
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name IN ('ClassGroupSchedules', 'ClassSessions');
SELECT name, is_unique, filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('ClassSessions');
GO