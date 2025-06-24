using System.Text.Json.Serialization;

namespace DemoSRP.Models
{
    public class LoginRequest
    {
        public string Username { get; set; }
        public string A { get; set; }
    }

    public class LoginResponse
    {
        public string B { get; set; }
        public string Salt { get; set; }
    }

    public class ClientProofRequest
    {
        public string Username { get; set; }
        public string M1 { get; set; }
    }

    public class ServerProofResponse
    {
        public string M2 { get; set; }
        public string Token { get; set; }
        public string IV { get; set; }
    }

    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Salt { get; set; }
        public string Verifier { get; set; }
    }
}