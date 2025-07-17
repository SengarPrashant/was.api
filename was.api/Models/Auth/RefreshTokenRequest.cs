namespace was.api.Models.Auth
{
    public class RefreshTokenRequest
    {
        public string UserName { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
