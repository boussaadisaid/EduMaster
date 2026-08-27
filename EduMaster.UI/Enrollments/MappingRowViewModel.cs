using EduMaster.UI.Common.MVVM;
using System.Collections.ObjectModel;

namespace EduMaster.UI.Enrollments;

/// <summary>صف خريطة الانتقال (تر-3): (مستوى + شعبة المصدر) ثابتان ← الهدف يُختار · تبديل المستوى يعيد ملء الشعب ويرجعها لـ«بلا شعبة» (لا تخمين عبر المستويات — روح D-59)</summary>
public sealed class MappingRowViewModel : BaseViewModel
{
    private readonly Func<int, List<RolloverStreamOption>> _streamsForLevel;
    private readonly Action _onChanged;

    public MappingRowViewModel(int sourceLevelId, int? sourceStreamId, string sourceLevelName, string? sourceStreamName,
        IReadOnlyList<RolloverLevelOption> targetLevels, Func<int, List<RolloverStreamOption>> streamsForLevel, Action onChanged)
    {
        SourceLevelId = sourceLevelId;
        SourceStreamId = sourceStreamId;
        SourceLabel = sourceStreamName is null ? $"{sourceLevelName} — بلا شعبة" : $"{sourceLevelName} — {sourceStreamName}";
        TargetLevels = targetLevels;
        _streamsForLevel = streamsForLevel;
        _onChanged = onChanged;
    }

    public int SourceLevelId { get; }
    public int? SourceStreamId { get; }
    public string SourceLabel { get; }
    public IReadOnlyList<RolloverLevelOption> TargetLevels { get; }
    public ObservableCollection<RolloverStreamOption> TargetStreams { get; } = new();

    private RolloverLevelOption? _selectedTargetLevel;
    public RolloverLevelOption? SelectedTargetLevel
    {
        get => _selectedTargetLevel;
        set
        {
            if (SetProperty(ref _selectedTargetLevel, value))
            {
                RebuildStreams();
                _onChanged();
            }
        }
    }

    private RolloverStreamOption? _selectedTargetStream;
    public RolloverStreamOption? SelectedTargetStream
    {
        get => _selectedTargetStream;
        set
        {
            if (SetProperty(ref _selectedTargetStream, value))
                _onChanged();
        }
    }

    /// <summary>الافتراضي الذكي يُحقن من الوالد بلا إطلاق سلسلة تغييرات (تهيئة فقط)</summary>
    public void ApplyDefaults(RolloverLevelOption? level, IEnumerable<RolloverStreamOption> streams, RolloverStreamOption? stream)
    {
        _selectedTargetLevel = level;
        OnPropertyChanged(nameof(SelectedTargetLevel));
        TargetStreams.Clear();
        foreach (var streamOption in streams)
            TargetStreams.Add(streamOption);
        _selectedTargetStream = stream;
        OnPropertyChanged(nameof(SelectedTargetStream));
    }

    private void RebuildStreams()
    {
        TargetStreams.Clear();
        if (SelectedTargetLevel is not null)
            foreach (var streamOption in _streamsForLevel(SelectedTargetLevel.Id))
                TargetStreams.Add(streamOption);
        SelectedTargetStream = TargetStreams.FirstOrDefault();   // «بلا شعبة» أول عنصر دائماً
    }
}