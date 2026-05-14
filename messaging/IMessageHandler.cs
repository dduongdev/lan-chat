using System.Text.Json;
using System.Threading.Tasks;

namespace LanChat.Messaging
{
    public interface IMessageHandler
    {
        string RoutingKey { get; }
        Task HandleAsync(SessionHandler session, JsonElement payload);
    }
}
