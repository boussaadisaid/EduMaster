using EduMaster.Application.Abstractions;



namespace EduMaster.Infrastructure.Security
{
    public sealed class BCryptPasswordHasher : IPasswordHasher
    {
        public string Hash(string password)        
             => BCrypt.Net.BCrypt.HashPassword(password);
        

        public bool Verify(string Password, string hash)
            => BCrypt.Net.BCrypt.Verify(Password, hash);
        
    }
}
