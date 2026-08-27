namespace EduMaster.Application.People;

/// <summary>مدخل فحص تكرار الاسم (6.6 — ز-2) — يُطبَّع بنفس دالة الكيان وبترتيب تركيبه حرفاً (الأول/اللقب/الأب — الكيان سطر 141)</summary>
public sealed record FindPersonDuplicateRequest(string FirstName, string LastName, string? FatherName);

/// <summary>مطابقة تكرار محتملة — لسطر التحذير غير المانع في المحرر</summary>
public sealed record PersonDuplicateMatch(int Id, string FullName);

/// <summary>سطر قراءة التطابق التام (IPersonRepository.GetByNormalizedFullNameAsync) — الاسم للعرض باتفاق ToString (الأول + اللقب)</summary>
public sealed record PersonDuplicateRaw(int Id, string FullName);
