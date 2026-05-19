using System;
using System.Buffers.Binary;

namespace LanChat.Shared.Media
{
    public enum CallMediaPacketType : byte
    {
        Video = 1,
        Audio = 2,
        Hello = 3,
        Ping = 4,
        Pong = 5
    }

    public enum CallMediaCodec : byte
    {
        None = 0,
        Mjpeg = 1,
        Pcm16 = 2
    }

    public sealed class CallMediaPacket
    {
        public const int HeaderLength = 48;
        public const int MaxDatagramSize = 1200;
        public const int MaxPayloadSize = MaxDatagramSize - HeaderLength;

        private const uint Magic = 0x4C435643; // LCVC
        private const byte Version = 1;

        public CallMediaPacketType PacketType { get; set; }
        public Guid CallId { get; set; }
        public ushort ParticipantId { get; set; }
        public uint Sequence { get; set; }
        public uint TimestampMs { get; set; }
        public uint FrameId { get; set; }
        public ushort FragmentIndex { get; set; }
        public ushort FragmentCount { get; set; } = 1;
        public CallMediaCodec Codec { get; set; }
        public byte Flags { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        public byte[] ToBytes()
        {
            if (Payload.Length > MaxPayloadSize)
            {
                throw new InvalidOperationException($"Payload cannot exceed {MaxPayloadSize} bytes.");
            }

            var bytes = new byte[HeaderLength + Payload.Length];
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), Magic);
            bytes[4] = Version;
            bytes[5] = (byte)PacketType;
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6, 2), HeaderLength);
            CallId.TryWriteBytes(bytes.AsSpan(8, 16));
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(24, 2), ParticipantId);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(26, 4), Sequence);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(30, 4), TimestampMs);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(34, 4), FrameId);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(38, 2), FragmentIndex);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(40, 2), FragmentCount);
            bytes[42] = (byte)Codec;
            bytes[43] = Flags;
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(44, 2), (ushort)Payload.Length);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(46, 2), 0);

            if (Payload.Length > 0)
            {
                Buffer.BlockCopy(Payload, 0, bytes, HeaderLength, Payload.Length);
            }

            return bytes;
        }

        public static bool TryParse(ReadOnlySpan<byte> bytes, out CallMediaPacket packet)
        {
            packet = new CallMediaPacket();
            if (bytes.Length < HeaderLength) return false;
            if (BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(0, 4)) != Magic) return false;
            if (bytes[4] != Version) return false;

            ushort headerLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(6, 2));
            if (headerLength != HeaderLength || bytes.Length < headerLength) return false;

            ushort payloadLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(44, 2));
            if (bytes.Length < HeaderLength + payloadLength) return false;

            packet.PacketType = (CallMediaPacketType)bytes[5];
            packet.CallId = new Guid(bytes.Slice(8, 16));
            packet.ParticipantId = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(24, 2));
            packet.Sequence = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(26, 4));
            packet.TimestampMs = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(30, 4));
            packet.FrameId = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(34, 4));
            packet.FragmentIndex = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(38, 2));
            packet.FragmentCount = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(40, 2));
            packet.Codec = (CallMediaCodec)bytes[42];
            packet.Flags = bytes[43];
            packet.Payload = bytes.Slice(HeaderLength, payloadLength).ToArray();
            return true;
        }
    }
}
