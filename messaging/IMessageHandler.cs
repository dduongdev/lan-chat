using System.Text.Json;
using System.Threading.Tasks;
using SimpleTcp;

namespace LanChat.Messaging
{
    public interface IMessageHandler
    {
        string RoutingKey { get; }
        Task HandleAsync(ISimpleTcpClient client, JsonElement payload);
    }
}
