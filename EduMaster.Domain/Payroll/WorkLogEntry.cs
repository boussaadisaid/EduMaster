using EduMaster.Domain.Common;

namespace EduMaster.Domain.Payroll
{
    /// <summary>
    /// يوم عمل موظف (D-115) — أساس الأجر اليومي غير المنتظم (موظفة التنظيف).
    /// كتابة فقط: التصحيح = حذف اليوم وإعادة تسجيله (لا Update) · حارس «لا تاريخ مستقبل» في الـHandler (الكيان لا يقرأ الساعة — D-20) ·
    /// فرادة (الموظف، اليوم) قاعدةً.
    /// </summary>
    public sealed class WorkLogEntry
    {
        public int Id { get; private set; }
        public int EmployeeId { get; private set; }
        public DateOnly WorkDate { get; private set; }
        public string? Note { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public int? CreatedByUserId { get; private set; }

        private bool _idSet;

        private WorkLogEntry(int employeeId, DateOnly workDate, string? note,
            DateTime createdAtUtc, int? createdByUserId)
        {
            if (employeeId <= 0)
                throw new DomainException("يوم العمل يجب أن يتبع موظفاً.");
            if (note?.Trim().Length > 200)
                throw new DomainException("ملاحظة اليوم طويلة جداً (الحد الأقصى 200 حرف).");

            EmployeeId = employeeId;
            WorkDate = workDate;
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            CreatedAtUtc = createdAtUtc;
            CreatedByUserId = createdByUserId;
        }

        private WorkLogEntry(int id, int employeeId, DateOnly workDate, string? note,
            DateTime createdAtUtc, int? createdByUserId)
            : this(employeeId, workDate, note, createdAtUtc, createdByUserId)
        {
            if (id <= 0)
                throw new DomainException("المعرف يجب أن يكون أكبر من صفر");

            Id = id;
            _idSet = true;
        }

        public static WorkLogEntry Create(int employeeId, DateOnly workDate, string? note,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new WorkLogEntry(employeeId, workDate, note, createdAtUtc, createdByUserId);
        }

        public static WorkLogEntry Load(int id, int employeeId, DateOnly workDate, string? note,
            DateTime createdAtUtc, int? createdByUserId)
        {
            return new WorkLogEntry(id, employeeId, workDate, note, createdAtUtc, createdByUserId);
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