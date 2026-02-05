namespace ComputerClub
{
    public static class AppConfig
    {
        public static bool IsOnSite { get; set; }
        public static int? DeviceNumber { get; set; }
        public static string DeviceName { get; set; }
        public static int? CurrentClientId { get; set; }
        public static string DeviceType { get; set; }

        public static void Reset()
        {
            IsOnSite = false;
            DeviceNumber = null;
            DeviceName = null;
            CurrentClientId = null;
        }
    }
}