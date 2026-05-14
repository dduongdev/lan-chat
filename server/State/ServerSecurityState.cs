using LanChat.Shared.Security;

namespace LanChat.Server.State
{
    public static class ServerSecurityState
    {
        // Singleton RsaManager cho Server
        public static readonly RsaManager RsaManager = new RsaManager();
    }
}
