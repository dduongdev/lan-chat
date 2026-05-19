using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LanChat.Shared.Media;
using LanChat.Shared.Payloads;

namespace LanChat.Client.Services
{
    public sealed class UdpMediaTransport : IDisposable
    {
        private readonly ConcurrentDictionary<ushort, IPEndPoint> _remoteEndpoints = new();
        private readonly ConcurrentDictionary<FrameKey, FragmentBuffer> _videoFrames = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly DateTime _startedAt = DateTime.UtcNow;

        private UdpClient? _udpClient;
        private Guid _callId;
        private ushort _participantId;
        private uint _sequence;
        private uint _frameId;

        public event Action<ushort, byte[]>? VideoFrameReceived;
        public event Action<ushort, byte[]>? AudioPacketReceived;
        public event Action<string>? StatusChanged;

        public int LocalPort { get; private set; }
        public bool IsConfigured => _callId != Guid.Empty && _participantId != 0;

        public void Open()
        {
            if (_udpClient != null) return;

            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            LocalPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;
            _ = Task.Run(ReceiveLoopAsync);
            _ = Task.Run(CleanupLoopAsync);
        }

        public void Configure(Guid callId, ushort participantId)
        {
            _callId = callId;
            _participantId = participantId;
        }

        public void UpdateParticipants(IEnumerable<CallParticipantDto> participants, string? currentUsername)
        {
            if (!IsConfigured) return;

            _remoteEndpoints.Clear();
            foreach (var participant in participants)
            {
                if (participant.ParticipantId == _participantId) continue;
                if (!string.IsNullOrEmpty(currentUsername) && participant.Username == currentUsername) continue;
                if (!IPAddress.TryParse(participant.IpAddress, out var address)) continue;

                _remoteEndpoints[participant.ParticipantId] = new IPEndPoint(address, participant.UdpPort);
            }

            SendHello();
            StatusChanged?.Invoke($"UDP peers: {_remoteEndpoints.Count}");
        }

        public async Task SendVideoFrameAsync(byte[] jpegBytes)
        {
            if (!IsConfigured || _udpClient == null || _remoteEndpoints.IsEmpty || jpegBytes.Length == 0) return;

            uint frameId = unchecked(++_frameId);
            ushort fragmentCount = (ushort)Math.Ceiling(jpegBytes.Length / (double)CallMediaPacket.MaxPayloadSize);
            if (fragmentCount == 0) fragmentCount = 1;

            for (ushort index = 0; index < fragmentCount; index++)
            {
                int offset = index * CallMediaPacket.MaxPayloadSize;
                int length = Math.Min(CallMediaPacket.MaxPayloadSize, jpegBytes.Length - offset);
                var payload = new byte[length];
                Buffer.BlockCopy(jpegBytes, offset, payload, 0, length);

                var packet = new CallMediaPacket
                {
                    PacketType = CallMediaPacketType.Video,
                    CallId = _callId,
                    ParticipantId = _participantId,
                    Sequence = unchecked(++_sequence),
                    TimestampMs = GetTimestampMs(),
                    FrameId = frameId,
                    FragmentIndex = index,
                    FragmentCount = fragmentCount,
                    Codec = CallMediaCodec.Mjpeg,
                    Payload = payload
                }.ToBytes();

                foreach (var endpoint in _remoteEndpoints.Values)
                {
                    await _udpClient.SendAsync(packet, packet.Length, endpoint);
                }
            }
        }

        public async Task SendAudioPacketAsync(byte[] pcmBytes)
        {
            if (!IsConfigured || _udpClient == null || _remoteEndpoints.IsEmpty || pcmBytes.Length == 0) return;
            if (pcmBytes.Length > CallMediaPacket.MaxPayloadSize) return;

            var packet = new CallMediaPacket
            {
                PacketType = CallMediaPacketType.Audio,
                CallId = _callId,
                ParticipantId = _participantId,
                Sequence = unchecked(++_sequence),
                TimestampMs = GetTimestampMs(),
                FrameId = unchecked(++_frameId),
                FragmentIndex = 0,
                FragmentCount = 1,
                Codec = CallMediaCodec.Pcm16,
                Payload = pcmBytes
            }.ToBytes();

            foreach (var endpoint in _remoteEndpoints.Values)
            {
                await _udpClient.SendAsync(packet, packet.Length, endpoint);
            }
        }

        public void SendHello()
        {
            if (!IsConfigured || _udpClient == null || _remoteEndpoints.IsEmpty) return;

            var packet = new CallMediaPacket
            {
                PacketType = CallMediaPacketType.Hello,
                CallId = _callId,
                ParticipantId = _participantId,
                Sequence = unchecked(++_sequence),
                TimestampMs = GetTimestampMs(),
                Codec = CallMediaCodec.None
            }.ToBytes();

            foreach (var endpoint in _remoteEndpoints.Values)
            {
                _ = _udpClient.SendAsync(packet, packet.Length, endpoint);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_udpClient == null)
                    {
                        await Task.Delay(50, _cts.Token);
                        continue;
                    }

                    var result = await _udpClient.ReceiveAsync(_cts.Token);
                    if (!CallMediaPacket.TryParse(result.Buffer, out var packet)) continue;
                    if (!IsConfigured || packet.CallId != _callId) continue;
                    if (packet.ParticipantId == _participantId) continue;

                    if (packet.PacketType == CallMediaPacketType.Hello)
                    {
                        StatusChanged?.Invoke($"UDP connected with participant {packet.ParticipantId}");
                    }
                    else if (packet.PacketType == CallMediaPacketType.Video && packet.Codec == CallMediaCodec.Mjpeg)
                    {
                        HandleVideoPacket(packet);
                    }
                    else if (packet.PacketType == CallMediaPacketType.Audio && packet.Codec == CallMediaCodec.Pcm16)
                    {
                        AudioPacketReceived?.Invoke(packet.ParticipantId, packet.Payload);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(50);
                }
            }
        }

        private void HandleVideoPacket(CallMediaPacket packet)
        {
            if (packet.FragmentCount <= 1)
            {
                VideoFrameReceived?.Invoke(packet.ParticipantId, packet.Payload);
                return;
            }

            var key = new FrameKey(packet.ParticipantId, packet.FrameId);
            var buffer = _videoFrames.GetOrAdd(key, _ => new FragmentBuffer(packet.FragmentCount));
            if (buffer.Add(packet.FragmentIndex, packet.Payload, out var frameBytes))
            {
                _videoFrames.TryRemove(key, out _);
                VideoFrameReceived?.Invoke(packet.ParticipantId, frameBytes);
            }
        }

        private async Task CleanupLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(500, _cts.Token);
                    var cutoff = DateTime.UtcNow.AddMilliseconds(-250);
                    foreach (var item in _videoFrames.ToArray())
                    {
                        if (item.Value.CreatedAt < cutoff)
                        {
                            _videoFrames.TryRemove(item.Key, out _);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private uint GetTimestampMs() => (uint)Math.Max(0, (DateTime.UtcNow - _startedAt).TotalMilliseconds);

        public void Dispose()
        {
            _cts.Cancel();
            _udpClient?.Dispose();
            _cts.Dispose();
            _remoteEndpoints.Clear();
            _videoFrames.Clear();
        }

        private readonly record struct FrameKey(ushort ParticipantId, uint FrameId);

        private sealed class FragmentBuffer
        {
            private readonly byte[][] _fragments;
            private readonly bool[] _received;
            private int _receivedCount;

            public DateTime CreatedAt { get; } = DateTime.UtcNow;

            public FragmentBuffer(ushort fragmentCount)
            {
                _fragments = new byte[fragmentCount][];
                _received = new bool[fragmentCount];
            }

            public bool Add(ushort index, byte[] payload, out byte[] frameBytes)
            {
                frameBytes = Array.Empty<byte>();
                if (index >= _fragments.Length) return false;

                if (!_received[index])
                {
                    _fragments[index] = payload;
                    _received[index] = true;
                    _receivedCount++;
                }

                if (_receivedCount != _fragments.Length) return false;

                int totalLength = _fragments.Sum(f => f.Length);
                frameBytes = new byte[totalLength];
                int offset = 0;
                foreach (var fragment in _fragments)
                {
                    Buffer.BlockCopy(fragment, 0, frameBytes, offset, fragment.Length);
                    offset += fragment.Length;
                }
                return true;
            }
        }
    }
}
