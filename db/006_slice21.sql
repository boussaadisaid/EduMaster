-- ============================================================
-- EduMaster — 006_slice21.sql
-- الشريحة 2.1: الأفواج (ClassGroup) — فوج مادة سنوي بشعب متعددة
-- التشغيل: SSMS بعد 005 — آمن للتكرار
-- القواعد المحسومة:
--   · فوج مادة سنوي: سنة + مستوى + مادة + أستاذ اختياري يُسنَد لاحقاً (D-47)
--   · شعب الفوج M:N عبر ClassGroupStreams — قائمة فارغة = يقبل كل شعب المستوى (D-48)
--   · طالب بلا شعبة يُمنع من الأفواج المقيّدة (D-59) — الحارس في الـHandler
--   · القاعة اختيارية دائماً (D-44) · السعة اختيارية > 0 — حارسها في 2.4
--   · تعطيل لا حذف — بلا IsDeleted (اتساق D-45)
--   · فرادة اسم الفوج داخل السنة الواحدة
--   · ضمان مزدوج بروح D-28 لتطابق الشعبة↔مستوى الفوج: FK مركّب في القاعدة + حارس Handler
-- ============================================================

USE EduMasterDb;
GO

-- =====================================================
-- دعامة الضمان المزدوج: فرادة (LevelId, Id) على الشعب
-- — شرط الـFK المركّب القادم من ClassGroupStreams
-- (آمنة على البيانات القائمة: Id وحده فريد أصلاً)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Streams_Level_Id' AND object_id = OBJECT_ID('Streams'))
BEGIN
    ALTER TABLE Streams ADD CONSTRAINT UQ_Streams_Level_Id UNIQUE (LevelId, Id);
END
GO

-- =====================================================
-- جدول الأفواج — فوج مادة سنوي
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassGroups')
BEGIN
    CREATE TABLE ClassGroups
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassGroups PRIMARY KEY,
        AcademicYearId      INT               NOT NULL,
        LevelId             INT               NOT NULL,
        SubjectId           INT               NOT NULL,
        TeacherId           INT               NULL,   -- اختياري: يُسنَد لاحقاً (D-47)
        RoomId              INT               NULL,   -- القاعة اختيارية دائماً (D-44)
        Name                NVARCHAR(100)     NOT NULL,
        NameNormalized      NVARCHAR(100)     NOT NULL,
        Capacity            INT               NULL,   -- سعة اختيارية — حارس التسجيل في 2.4

        IsActive            BIT               NOT NULL CONSTRAINT DF_ClassGroups_IsActive DEFAULT (1),

        CreatedAtUtc        DATETIME2         NOT NULL CONSTRAINT DF_ClassGroups_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId     INT               NULL,
        UpdatedAtUtc        DATETIME2         NULL,
        UpdatedByUserId     INT               NULL,

        CONSTRAINT FK_ClassGroups_AcademicYears FOREIGN KEY (AcademicYearId) REFERENCES AcademicYears(Id),
        CONSTRAINT FK_ClassGroups_Levels        FOREIGN KEY (LevelId)        REFERENCES Levels(Id),
        CONSTRAINT FK_ClassGroups_Subjects      FOREIGN KEY (SubjectId)      REFERENCES Subjects(Id),
        CONSTRAINT FK_ClassGroups_Teachers      FOREIGN KEY (TeacherId)      REFERENCES Teachers(Id),
        CONSTRAINT FK_ClassGroups_Rooms         FOREIGN KEY (RoomId)         REFERENCES Rooms(Id),
        CONSTRAINT UQ_ClassGroups_Year_Name     UNIQUE (AcademicYearId, Name),
        CONSTRAINT CK_ClassGroups_Capacity      CHECK (Capacity IS NULL OR Capacity > 0),
        CONSTRAINT UQ_ClassGroups_Id_Level      UNIQUE (Id, LevelId)   -- دعامة الـFK المركّب من ClassGroupStreams
    );
END
GO

-- =====================================================
-- جدول شعب الفوج (M:N) — فارغ = يقبل كل شعب المستوى (D-48)
-- LevelId مخزَّن من الفوج لفرض تطابق الشعبة مع مستوى الفوج قاعدةً (بروح D-28):
--   · FK مركّب (ClassGroupId, LevelId) ← ClassGroups(Id, LevelId): يمنع انحراف LevelId عن مستوى الفوج
--   · FK مركّب (LevelId, StreamId)   ← Streams(LevelId, Id):     يمنع شعبة من غير مستوى الفوج
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassGroupStreams')
BEGIN
    CREATE TABLE ClassGroupStreams
    (
        ClassGroupId INT NOT NULL,
        LevelId      INT NOT NULL,
        StreamId     INT NOT NULL,

        CONSTRAINT PK_ClassGroupStreams PRIMARY KEY (ClassGroupId, StreamId),
        CONSTRAINT FK_ClassGroupStreams_ClassGroups FOREIGN KEY (ClassGroupId, LevelId) REFERENCES ClassGroups(Id, LevelId),
        CONSTRAINT FK_ClassGroupStreams_Streams     FOREIGN KEY (LevelId, StreamId)     REFERENCES Streams(LevelId, Id)
    );
END
GO

-- تحقق سريع
SELECT name FROM sys.tables WHERE name IN ('ClassGroups', 'ClassGroupStreams') ORDER BY name;
GO