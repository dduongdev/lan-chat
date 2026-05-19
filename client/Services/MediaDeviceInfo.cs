namespace LanChat.Client.Services
{
    public sealed class MediaDeviceInfo
    {
        public int DeviceIndex { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string DevicePath { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }
}
