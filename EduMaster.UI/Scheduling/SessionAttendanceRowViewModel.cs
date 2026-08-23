using EduMaster.Domain.Enums;
using EduMaster.UI.Common.MVVM;

namespace EduMaster.UI.Scheduling;

/// <summary>صف واحد في ديالوغ الحضور — ثلاثية الاختيار (D-93) بأزرار مقسّمة (لا RadioButtons: تجميعها داخل صفوف الشبكة هشّ)</summary>
public sealed class SessionAttendanceRowViewModel : BaseViewModel
{
    private readonly Action _onStatusChanged;

    public SessionAttendanceRowViewModel(int enrollmentId, string fullName, AttendanceStatus status, string? note, Action onStatusChanged)
    {
        EnrollmentId = enrollmentId;
        FullName = fullName;
        _status = status;
        _note = note ?? string.Empty;
        _onStatusChanged = onStatusChanged;

        SetPresentCommand = new AsyncRelayCommand(() => { Status = AttendanceStatus.Present; return Task.CompletedTask; });
        SetAbsentCommand = new AsyncRelayCommand(() => { Status = AttendanceStatus.Absent; return Task.CompletedTask; });
        SetJustifiedCommand = new AsyncRelayCommand(() => { Status = AttendanceStatus.Justified; return Task.CompletedTask; });
    }

    public int EnrollmentId { get; }
    public string FullName { get; }

    private AttendanceStatus _status;
    public AttendanceStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsPresent));
                OnPropertyChanged(nameof(IsAbsent));
                OnPropertyChanged(nameof(IsJustified));
                _onStatusChanged();
            }
        }
    }

    public bool IsPresent => Status == AttendanceStatus.Present;
    public bool IsAbsent => Status == AttendanceStatus.Absent;
    public bool IsJustified => Status == AttendanceStatus.Justified;

    private string _note;
    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public AsyncRelayCommand SetPresentCommand { get; }
    public AsyncRelayCommand SetAbsentCommand { get; }
    public AsyncRelayCommand SetJustifiedCommand { get; }
}