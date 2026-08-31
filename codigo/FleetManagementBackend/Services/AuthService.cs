namespace FleetManagementBackend.Services
{
    public class AuthService : IAuthService
    {
        public bool ValidateUser(string username, string password)
        {
            // Credenciales simuladas para el prototipo
            return username == "admin" && password == "1234";
        }
    }
}