using EduMaster.Application.Abstractions;



namespace EduMaster.UI.Services
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        public int? UserAccountId { get; private set; }   // القراءة للجميع عبر الواجهة
        public string? Username { get; private set; }

        // التغيير فقط لمن يملك الكلاس الحقيقي (الواجهة) — الواجهة لا تملك SignIn
        public void SignIn(int userAccountId, string username)
        {
            UserAccountId = userAccountId;
            Username = username;
        }

        public void SignOut()
        {
            UserAccountId = null;
            Username = null;
        }


    }
}
