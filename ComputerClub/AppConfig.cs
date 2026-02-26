namespace ComputerClub
{
    public static class AppConfig
    {
        public static bool IsOnSite { get; set; }
        public static int? DeviceNumber { get; set; }
        public static string DeviceName { get; set; }
        public static string DeviceType { get; set; } // "PC" или "Console"
        public static int? CurrentClientId { get; set; }
        public static int? TariffID { get; set; }
        public static bool IsSessionActive { get; set; } = false;
        public static bool IsDeviceSwitchInProgress { get; set; } = false;
        public static bool NavigateToBookingAfterLogin { get; set; } = false;

        public static void Reset()
        {
            IsOnSite = false;
            DeviceNumber = null;
            DeviceName = null;
            DeviceType = null;
            CurrentClientId = null;
        }
        public static void ResetDeviceOnly()
        {
            IsOnSite = false;
            DeviceNumber = null;
            DeviceName = null;
            DeviceType = null;
            TariffID = null;
            // CurrentClientId НЕ сбрасываем!
            // IsDeviceSwitchInProgress можно сбросить позже
        }
        // Удобный метод для быстрого получения имени устройства
        public static string GetDeviceDisplay()
        {
            if (!IsOnSite) return "Удалённый доступ";
            if (DeviceNumber.HasValue && !string.IsNullOrEmpty(DeviceName))
                return $"№{DeviceNumber} • {DeviceName}";
            return "Устройство подключено";
        }
    }
}