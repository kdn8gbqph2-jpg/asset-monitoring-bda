namespace asset_monitoring.Models
{
    public class ActiveUsers
    {
        public int UserId { get; set; } = new();
        public string Name { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;

    }

}
