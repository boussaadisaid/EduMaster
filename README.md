<div align="center">


# 🏫 EduMaster

**An Arabic (RTL) desktop school management system for private tutoring and support schools**

People · Groups · Sessions · Attendance · Finance · Teacher Payments — in one platform

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-MVVM-0C54A3)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2019+-CC2927?logo=microsoftsqlserver&logoColor=white)
![Dapper](https://img.shields.io/badge/Data-Dapper-blue)
![Architecture](https://img.shields.io/badge/Architecture-Clean-success)

</div>

---

## ✨ Current Status — v1.0 (Active Development)

Completed so far: **the system backbone + a complete login feature**

- 🔐 Secure password storage using **BCrypt** — passwords are never stored as plain text
- 🔒 Automatic account lockout after 5 failed login attempts
- 🌱 Database initialization and administrator account seeding on first run — handled through a **Hosted Service**
- 🩺 Live database connection health check with retry from the login screen
- 🔔 Toast notifications + clear Arabic error messages within the forms
- 📝 Full event and error logging with **Serilog** (daily files, 14-day retention)
- 🛡 Comprehensive unhandled-exception handling — errors are logged instead of failing silently

## 🧱 Technologies

| Area | Choice |
|---|---|
| Language / Framework | C# — .NET 8 |
| UI | WPF · MVVM · MahApps.Metro · IconPacks |
| Database | SQL Server |
| Data Access | **Dapper** over ADO.NET — explicit SQL without a heavy ORM |
| Password Hashing | BCrypt.Net-Next |
| Logging | Serilog (daily files) |
| Hosting / Dependency Injection | Microsoft Generic Host + DI |
| Notifications | ToastNotifications behind the `IUserNotifier` abstraction |

## 🏗 Architecture

**Clean Architecture** with four layers — dependencies always point inward, and the Domain knows nothing about the outer layers:

```text
src/
├── EduMaster.Domain            ← Rich entities + Value Objects + business rules (zero dependencies)
├── EduMaster.Application       ← Use Cases (Handlers) + abstractions + OperationResult
├── EduMaster.Infrastructure    ← Dapper Repositories + UnitOfWork + password hashing + initialization
└── EduMaster.UI                ← WPF Views/ViewModels + Composition Root 




 ======================================================================================================
 <div align="center">

# 🏫 EduMaster

**نظام مكتبي عربي (RTL) لإدارة مدرسة خاصة للدعم والتقوية**

الأشخاص · الأفواج · الحصص · الحضور · المالية · مستحقات الأساتذة — في منصّة واحدة

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-MVVM-0C54A3)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2019+-CC2927?logo=microsoftsqlserver&logoColor=white)
![Dapper](https://img.shields.io/badge/Data-Dapper-blue)
![Architecture](https://img.shields.io/badge/Architecture-Clean-success)

</div>

---

## ✨ الحالة الحالية — v1.0 (تطوير نشط)

المنجز حتى الآن: **العمود الفقري للنظام + فيتشر تسجيل الدخول كاملة**

- 🔐 دخول آمن ببصمات **BCrypt** — لا تُخزَّن كلمة مرور نصياً في أي مكان
- 🔒 قفل الحساب تلقائياً بعد 5 محاولات فاشلة
- 🌱 تهيئة قاعدة البيانات وزرع حساب المدير عند أول تشغيل — عبر **Hosted Service**، دون إسقاط التطبيق عند غياب القاعدة
- 🩺 فحص اتصال حي بقاعدة البيانات مع إعادة محاولة من شاشة الدخول
- 🔔 إشعارات Toast + رسائل أخطاء عربية واضحة داخل النماذج
- 📝 سجل أحداث وأخطاء كامل بـ **Serilog** (ملفات يومية، احتفاظ 14 يوماً)
- 🛡 معالجة شاملة للاستثناءات العامة — لا شيء يموت بصمت

## 🧱 التقنيات

| المجال | الاختيار |
|---|---|
| اللغة / الإطار | C# — .NET 8 |
| الواجهة | WPF · MVVM · MahApps.Metro · IconPacks |
| قاعدة البيانات | SQL Server |
| الوصول للبيانات | **Dapper** فوق ADO.NET — SQL صريح بلا ORM ثقيل |
| التشفير | BCrypt.Net-Next |
| السجلات | Serilog (ملفات يومية) |
| الاستضافة والحقن | Microsoft Generic Host + DI |
| الإشعارات | ToastNotifications خلف واجهة `IUserNotifier` |

## 🏗 المعمارية

**Clean Architecture** بأربع طبقات — الاعتماد يتجه للداخل دائماً، وDomain لا يعرف أحداً:

```
src/
├── EduMaster.Domain            ← كيانات غنية + Value Objects + قواعد العمل (صفر اعتماديات)
├── EduMaster.Application       ← Use Cases (Handlers) + التجريدات + OperationResult
├── EduMaster.Infrastructure    ← Dapper Repositories + UnitOfWork + التشفير + التهيئة
└── EduMaster.UI                ← WPF Views/ViewModels + Composition Root
```

### المبادئ الحاكمة

- كل عملية تمر عبر **Use Case** واضح داخل **Unit of Work** واحدة (ذرّية كاملة)
- **Scope-per-Use-Case** في الواجهة — كل عملية في Scope خاص يُغلق فور انتهائها
- الدومين **لا يقرأ الساعة** — الوقت يُمرَّر عبر `IClock`
- نتائج العمليات عبر `OperationResult` مع تصنيف الخطأ `ErrorType` (الواجهة تقرر أين تعرضه)
- رسائل المستخدم **عربية**؛ السجلات والاستثناءات **إنجليزية تقنية**
- الرصيد والحالة المشتقة **تُشتق ولا تُعدَّل يدوياً**

## 🚀 البدء السريع

### المتطلبات

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server (2019+ أو Express)
- Visual Studio 2022

### أول تشغيل

```bash
git clone https://github.com/<اسم-المستخدم>/SchoolSys.git
cd SchoolSys
```

1. نفّذ السكربت `db/001_init.sql` في SSMS لإنشاء قاعدة البيانات والجداول.
2. راجع `appsettings.json` وتأكد من صحة Connection String.
3. شغّل التطبيق.
4. عند أول تشغيل، يقوم التطبيق بتهيئة البيانات الأولية وزرع حساب المدير.

### 🔑 الدخول الافتراضي

```
المستخدم: admin
كلمة المرور: admin123
```

> ⚠️ غيّر كلمة المرور الافتراضية فور توفر شاشة «تغيير كلمة المرور» (ضمن خارطة الطريق).

## 📏 اتفاقيات الكود

- أسماء الكيانات والكلاسات إنجليزية؛ نصوص الواجهة عربية
- كل كلاس لا يُراد الوراثة منه: `sealed`
- كل فيتشر في مجلدها الخاص داخل كل طبقة
- لا `DateTime.Now` خارج Infrastructure — أبداً
- كل التزام (Commit) يمثل خطوة مكتملة قابلة للعمل

## 🗺 خارطة الطريق

- [x] **F1.0** العمود الفقري (طبقات + DI + UoW + Serilog + فحص الاتصال)
- [x] **F1.1** تسجيل الدخول الكامل (BCrypt + قفل + بذر + صمود)
- [ ] **F1.2** السنة الدراسية
- [ ] **F1.3** الأشخاص + المستخدمون والصلاحيات
- [ ] **F1.4** ملفات الطلاب والأساتذة
- [ ] **F1.5** البنية الأكاديمية (مستويات، شعب، مواد، قاعات)
- [ ] **F2** الأفواج والتسجيل
- [ ] **F3** الحصص والحضور
- [ ] **F4** المالية (استحقاقات، قبض، ديون)
- [ ] **F5** مستحقات الأساتذة
- [ ] **F6** التقارير والإدارة

## 📁 مستودع قاعدة البيانات

سكربتات SQL مرقّمة داخل `db/` — كل تغيير بنيوي = سكربت جديد برقم تسلسلي. لا تعديل على سكربتات منفَّذة.

---

<div align="center">

**المطوّر:** سعيد بوسعادي — مشروع خاص، جميع الحقوق محفوظة © 2026

</div>
