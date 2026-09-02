/* ============================================================================
   021_sms.sql — SMS foundation + TextBee integration records
   - No Sender ID is stored in EduMaster.
   - Provider credentials are stored locally/securely by Infrastructure.
   - Messages are append-only records; status changes preserve the audit trail.
   ============================================================================ */

IF OBJECT_ID(N'dbo.SmsTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsTemplates
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsTemplates PRIMARY KEY,
        Name              NVARCHAR(100) NOT NULL,
        Category          TINYINT NOT NULL,
        Body              NVARCHAR(1000) NOT NULL,
        IsActive          BIT NOT NULL CONSTRAINT DF_SmsTemplates_IsActive DEFAULT (1),
        CreatedAtUtc      DATETIME2(0) NOT NULL,
        CreatedByUserId   INT NULL,
        UpdatedAtUtc      DATETIME2(0) NULL,
        UpdatedByUserId   INT NULL,
        CONSTRAINT CK_SmsTemplates_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
        CONSTRAINT CK_SmsTemplates_Body_NotBlank CHECK (LEN(LTRIM(RTRIM(Body))) > 0),
        CONSTRAINT CK_SmsTemplates_Category CHECK (Category BETWEEN 1 AND 6)
    );

    CREATE UNIQUE INDEX UX_SmsTemplates_Name
        ON dbo.SmsTemplates(Name);
END;
GO

IF OBJECT_ID(N'dbo.SmsBatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsBatches
    (
        Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsBatches PRIMARY KEY,
        Category              TINYINT NOT NULL,
        TemplateId            INT NULL,
        ProviderBatchId       NVARCHAR(100) NULL,
        DeviceId              NVARCHAR(100) NULL,
        Status                TINYINT NOT NULL,
        TotalCount            INT NOT NULL,
        SubmittedCount        INT NOT NULL CONSTRAINT DF_SmsBatches_SubmittedCount DEFAULT (0),
        DeliveredCount        INT NOT NULL CONSTRAINT DF_SmsBatches_DeliveredCount DEFAULT (0),
        FailedCount           INT NOT NULL CONSTRAINT DF_SmsBatches_FailedCount DEFAULT (0),
        CreatedAtUtc          DATETIME2(0) NOT NULL,
        CreatedByUserId       INT NULL,
        LastSyncedAtUtc       DATETIME2(0) NULL,
        CONSTRAINT FK_SmsBatches_Template FOREIGN KEY (TemplateId) REFERENCES dbo.SmsTemplates(Id),
        CONSTRAINT CK_SmsBatches_Category CHECK (Category BETWEEN 1 AND 6),
        CONSTRAINT CK_SmsBatches_Status CHECK (Status BETWEEN 1 AND 5),
        CONSTRAINT CK_SmsBatches_Counts CHECK
        (
            TotalCount > 0 AND
            SubmittedCount >= 0 AND
            DeliveredCount >= 0 AND
            FailedCount >= 0 AND
            SubmittedCount <= TotalCount AND
            DeliveredCount <= SubmittedCount AND
            FailedCount <= TotalCount
        )
    );

    CREATE UNIQUE INDEX UX_SmsBatches_ProviderBatchId
        ON dbo.SmsBatches(ProviderBatchId)
        WHERE ProviderBatchId IS NOT NULL;
    CREATE INDEX IX_SmsBatches_CreatedAtUtc
        ON dbo.SmsBatches(CreatedAtUtc DESC, Id DESC);
END;
GO

IF OBJECT_ID(N'dbo.SmsMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsMessages
    (
        Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsMessages PRIMARY KEY,
        BatchId               INT NOT NULL,
        PersonId              INT NULL,
        StudentId             INT NULL,
        PhoneNumber           NVARCHAR(20) NOT NULL,
        MessageBody           NVARCHAR(1000) NOT NULL,
        TemplateId            INT NULL,
        Status                TINYINT NOT NULL,
        ProviderMessageId     NVARCHAR(100) NULL,
        ProviderStatus        NVARCHAR(50) NULL,
        ProviderErrorCode     NVARCHAR(100) NULL,
        CreatedAtUtc          DATETIME2(0) NOT NULL,
        SubmittedAtUtc        DATETIME2(0) NULL,
        SentAtUtc             DATETIME2(0) NULL,
        DeliveredAtUtc        DATETIME2(0) NULL,
        FailedAtUtc           DATETIME2(0) NULL,
        LastErrorMessage      NVARCHAR(500) NULL,
        RetryCount            INT NOT NULL CONSTRAINT DF_SmsMessages_RetryCount DEFAULT (0),
        CONSTRAINT FK_SmsMessages_Batch FOREIGN KEY (BatchId) REFERENCES dbo.SmsBatches(Id),
        CONSTRAINT FK_SmsMessages_Person FOREIGN KEY (PersonId) REFERENCES dbo.Persons(Id),
        CONSTRAINT FK_SmsMessages_Student FOREIGN KEY (StudentId) REFERENCES dbo.Students(Id),
        CONSTRAINT FK_SmsMessages_Template FOREIGN KEY (TemplateId) REFERENCES dbo.SmsTemplates(Id),
        CONSTRAINT CK_SmsMessages_Status CHECK (Status BETWEEN 1 AND 5),
        CONSTRAINT CK_SmsMessages_RetryCount CHECK (RetryCount >= 0),
        CONSTRAINT CK_SmsMessages_Phone_NotBlank CHECK (LEN(LTRIM(RTRIM(PhoneNumber))) >= 10),
        CONSTRAINT CK_SmsMessages_Body_NotBlank CHECK (LEN(LTRIM(RTRIM(MessageBody))) > 0)
    );

    CREATE INDEX IX_SmsMessages_BatchId
        ON dbo.SmsMessages(BatchId, Id);
    CREATE INDEX IX_SmsMessages_CreatedAtUtc
        ON dbo.SmsMessages(CreatedAtUtc DESC, Id DESC);
    CREATE INDEX IX_SmsMessages_Status
        ON dbo.SmsMessages(Status, CreatedAtUtc DESC, Id DESC);
END;
GO

IF OBJECT_ID(N'dbo.SmsDeliveryEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmsDeliveryEvents
    (
        Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmsDeliveryEvents PRIMARY KEY,
        SmsMessageId         INT NOT NULL,
        Status                TINYINT NOT NULL,
        ProviderStatus        NVARCHAR(50) NULL,
        ProviderErrorCode     NVARCHAR(100) NULL,
        OccurredAtUtc         DATETIME2(0) NOT NULL,
        RawPayload            NVARCHAR(MAX) NULL,
        CONSTRAINT FK_SmsDeliveryEvents_Message FOREIGN KEY (SmsMessageId) REFERENCES dbo.SmsMessages(Id),
        CONSTRAINT CK_SmsDeliveryEvents_Status CHECK (Status BETWEEN 1 AND 5)
    );

    CREATE INDEX IX_SmsDeliveryEvents_Message
        ON dbo.SmsDeliveryEvents(SmsMessageId, OccurredAtUtc DESC, Id DESC);
END;
GO

/* Seed only when the table is empty. Templates remain editable/disableable. */
IF NOT EXISTS (SELECT 1 FROM dbo.SmsTemplates)
BEGIN
    INSERT INTO dbo.SmsTemplates (Name, Category, Body, IsActive, CreatedAtUtc)
    VALUES
    (N'تذكير بالدين', 1,
     N'السلام عليكم، نعلمكم أن مستحقات ابنكم {StudentName} المقدرة بـ {Amount} دج لم تُسدّد بعد. يرجى التسديد في أقرب الآجال. {SchoolName}',
     1, SYSUTCDATETIME()),
    (N'تأكيد الدفع', 2,
     N'السلام عليكم، تم تسجيل دفع مبلغ {Amount} دج لفائدة {StudentName}. نشكركم على ثقتكم. {SchoolName}',
     1, SYSUTCDATETIME()),
    (N'إشعار الغياب', 3,
     N'السلام عليكم، نحيطكم علماً بغياب {StudentName} عن حصة {SubjectName} بتاريخ {Date}. {SchoolName}',
     1, SYSUTCDATETIME()),
    (N'نهاية الحصص', 4,
     N'السلام عليكم، نحيطكم علماً بأن رصيد الحصص المتبقي للطالب {StudentName} قد انتهى. يرجى تجديد الرصيد. {SchoolName}',
     1, SYSUTCDATETIME()),
    (N'رسالة إدارية', 5,
     N'السلام عليكم، {Message} مع تحيات {SchoolName}.',
     1, SYSUTCDATETIME()),
    (N'رسالة عامة', 6,
     N'السلام عليكم، {Message} مع تحيات {SchoolName}.',
     1, SYSUTCDATETIME());
END;
GO
