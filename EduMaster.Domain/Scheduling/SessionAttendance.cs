using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;

namespace EduMaster.Domain.Scheduling
{
    /// <summary>
    /// سطر حضور: (حصة × تسجيل فوج) بحالة ثلاثية (D-93).
    /// ملاحظة تصميمية: بلا Load — لا قراءة كيانية له أبداً؛ التصحيح استبدال ذرّي (حذف + إدراج — D-101)
    /// وقراءات الديالوغ مسطّحة (D-40)، فالكيان يخدم مسار الكتابة فقط.
    /// </summary>
    public sealed class SessionAttendance
    {
        public int Id { get; private set; }
        public int ClassSessionId { get; private set; }
        public int ClassGroupEnrollmentId { get; private set; }
        public AttendanceStatus Status { get; private set; }
        public string? Note { get; private set; }
        public DateTime MarkedAtUtc { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public DateTime? UpdatedAtUtc { get; private set; }
        public int? UpdatedByUserId { get; private set; }

        private bool _idSet;

        private SessionAttendance(int classSessionId, int classGroupEnrollmentId, AttendanceStatus status, string? note,
            DateTime markedAtUtc, DateTime createdAtUtc, int? createdByUserId, DateTime? updatedAtUtc, int? updatedByUserId)
        {
            if (classSessionId <= 0)
                throw new DomainException("سطر الحضور يجب أن يتبع حصة.");
            if (classGroupEnrollmentId <= 0)
                throw new DomainException("سطر الحضور يجب أن يتبع تسجيل فوج.");
            if (!Enum.IsDefined(status))
                throw new DomainException("حالة الحضور غير صالحة.");
            if (note is not null && note.Trim().Length > 200)
                throw new DomainException("الملاحظة طويلة جداً (الحد الأقصى 200 حرف).");

            ClassSessionId = classSessionId;
            ClassGroupEnrollmentId = classGroupEnrollmentId;
            Status = status;
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            MarkedAtUtc = markedAtUtc;
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
            UpdatedAtUtc = updatedAtUtc;
            UpdatedByUserId = updatedByUserId;
        }

        public static SessionAttendance Create(int classSessionId, int classGroupEnrollmentId, AttendanceStatus status, string? note,
            DateTime utcNow, int? createdByUserId)
        {
            return new SessionAttendance(classSessionId, classGroupEnrollmentId, status, note,
                utcNow, utcNow, createdByUserId, null, null);
        }

        internal void SetId(int id)
        {
            if (_idSet) throw new DomainException("لا يمكن تغيير المعرف بعد تعيينه");
            if (id <= 0) throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }
    }
}