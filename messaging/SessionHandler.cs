
namespace LanChat.Messaging
{
    public class SessionHandler
    {
        private readonly ISimpleTcpClient _client;
        private readonly MessageDispatcher _dispatcher;
        private bool _isRunning;

        public SessionHandler(ISimpleTcpClient client, MessageDispatcher dispatcher)
        {
            _client = client;
            _dispatcher = dispatcher;
        }

        public async Task StartAsync(CancellationToken ct = default)
        {
            _isRunning = true;
            try
            {
                while (_isRunning && !ct.IsCancellationRequested)
                {
                    var envelope = await _client.ReceiveObjectAsync<MessageEnvelope>();
                    
                    await _dispatcher.DispatchAsync(_client, envelope);
                }
            }
            catch (ConnectionClosedException)
            {
                Console.WriteLine("Client disconnected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Session error: {ex.Message}");
            }
            finally
            {
                _client.Close();
            }
        }

        public void Stop() => _isRunning = false;
    }

}