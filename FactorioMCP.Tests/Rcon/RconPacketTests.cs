using System.Buffers.Binary;
using System.Text;
using FactorioMCP.Rcon;
using Xunit;

namespace FactorioMCP.Tests.Rcon;

public class RconPacketTests
{
    [Fact]
    public void ToBytes_AuthPacket_ProducesCorrectWireFormat()
    {
        var packet = new RconPacket(1, RconPacketType.Auth, "password");
        var bytes = packet.ToBytes();

        // size field: 4 (id) + 4 (type) + 8 (body "password") + 2 (nulls) = 18
        var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        Assert.Equal(18, size);

        // total length = 4 (size field) + 18 = 22
        Assert.Equal(22, bytes.Length);

        // id
        var id = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        Assert.Equal(1, id);

        // type = Auth (3)
        var type = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8, 4));
        Assert.Equal(3, type);

        // body
        var body = Encoding.UTF8.GetString(bytes, 12, 8);
        Assert.Equal("password", body);

        // two null terminators
        Assert.Equal(0, bytes[^2]);
        Assert.Equal(0, bytes[^1]);
    }

    [Fact]
    public void ToBytes_ExecCommandPacket_HasCorrectType()
    {
        var packet = new RconPacket(42, RconPacketType.ExecCommand, "/c rcon.print('hello')");
        var bytes = packet.ToBytes();

        var type = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8, 4));
        Assert.Equal(2, type);
    }

    [Fact]
    public void ToBytes_ResponseValuePacket_HasCorrectType()
    {
        var packet = new RconPacket(5, RconPacketType.ResponseValue, "OK");
        var bytes = packet.ToBytes();

        var type = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8, 4));
        Assert.Equal(0, type);
    }

    [Fact]
    public void ToBytes_EmptyBody_ProducesMinimumSizePacket()
    {
        var packet = new RconPacket(1, RconPacketType.ExecCommand, "");
        var bytes = packet.ToBytes();

        // size = 4 (id) + 4 (type) + 0 (body) + 2 (nulls) = 10
        var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        Assert.Equal(RconPacket.MinPayloadSize, size);

        // total = 4 + 10 = 14
        Assert.Equal(14, bytes.Length);

        Assert.Equal(0, bytes[^2]);
        Assert.Equal(0, bytes[^1]);
    }

    [Fact]
    public void ToBytes_NegativeId_IsPreserved()
    {
        var packet = new RconPacket(-1, RconPacketType.ResponseValue, "");
        var bytes = packet.ToBytes();

        var id = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        Assert.Equal(-1, id);
    }

    [Fact]
    public void ToBytes_AllIntegersAreLittleEndian()
    {
        var packet = new RconPacket(0x01020304, RconPacketType.Auth, "");
        var bytes = packet.ToBytes();

        // id bytes at offset 4: little-endian 0x01020304 → [04, 03, 02, 01]
        Assert.Equal(0x04, bytes[4]);
        Assert.Equal(0x03, bytes[5]);
        Assert.Equal(0x02, bytes[6]);
        Assert.Equal(0x01, bytes[7]);
    }

    [Fact]
    public void ToBytes_Utf8Body_IsEncodedCorrectly()
    {
        var packet = new RconPacket(1, RconPacketType.ExecCommand, "héllo");
        var bytes = packet.ToBytes();
        var expectedBodyBytes = Encoding.UTF8.GetBytes("héllo");

        // size = 4 + 4 + expectedBodyBytes.Length + 2
        var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        Assert.Equal(4 + 4 + expectedBodyBytes.Length + 2, size);

        var bodySlice = bytes.AsSpan(12, expectedBodyBytes.Length);
        Assert.True(bodySlice.SequenceEqual(expectedBodyBytes));
    }

    [Fact]
    public void FromPayload_ValidAuthResponse_ParsesCorrectly()
    {
        // Build a payload: [id=7][type=ResponseValue(0)][body=""][null][null]
        var payload = new byte[RconPacket.MinPayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), 7);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), (int)RconPacketType.ResponseValue);

        var packet = RconPacket.FromPayload(payload);

        Assert.Equal(7, packet.Id);
        Assert.Equal(RconPacketType.ResponseValue, packet.Type);
        Assert.Equal(string.Empty, packet.Body);
    }

    [Fact]
    public void FromPayload_WithBody_ParsesBodyCorrectly()
    {
        var bodyBytes = Encoding.UTF8.GetBytes("Hello RCON");
        var payload = new byte[RconPacket.MinPayloadSize + bodyBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), (int)RconPacketType.ResponseValue);
        bodyBytes.CopyTo(payload, 8);

        var packet = RconPacket.FromPayload(payload);

        Assert.Equal(3, packet.Id);
        Assert.Equal("Hello RCON", packet.Body);
    }

    [Fact]
    public void FromPayload_FailedAuth_ParsesNegativeId()
    {
        // Auth failure returns id = -1
        var payload = new byte[RconPacket.MinPayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), -1);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), (int)RconPacketType.ResponseValue);

        var packet = RconPacket.FromPayload(payload);

        Assert.Equal(-1, packet.Id);
    }

    [Fact]
    public void FromPayload_TooSmall_Throws()
    {
        var tooSmall = new byte[9]; // minimum is 10

        var ex = Assert.Throws<InvalidOperationException>(() => RconPacket.FromPayload(tooSmall));
        Assert.Contains("too small", ex.Message);
    }

    [Fact]
    public void FromPayload_EmptySpan_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RconPacket.FromPayload(ReadOnlySpan<byte>.Empty));
    }

    [Theory]
    [InlineData(0)]  // ResponseValue
    [InlineData(2)]  // ExecCommand
    [InlineData(3)]  // Auth
    public void RoundTrip_AllPacketTypes_PreserveData(int typeValue)
    {
        var type = (RconPacketType)typeValue;
        var original = new RconPacket(99, type, "test body content");
        var bytes = original.ToBytes();

        // Skip the 4-byte size prefix to get the payload
        var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        var payload = bytes.AsSpan(4, size);

        var deserialized = RconPacket.FromPayload(payload);

        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Body, deserialized.Body);
    }

    [Fact]
    public void RoundTrip_EmptyBody_PreservesData()
    {
        var original = new RconPacket(1, RconPacketType.ExecCommand, "");
        var bytes = original.ToBytes();

        var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        var deserialized = RconPacket.FromPayload(bytes.AsSpan(4, size));

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void RoundTrip_LargeBody_PreservesData()
    {
        var largeBody = new string('X', 4000);
        var original = new RconPacket(10, RconPacketType.ExecCommand, largeBody);
        var bytes = original.ToBytes();

        var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        var deserialized = RconPacket.FromPayload(bytes.AsSpan(4, size));

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void RoundTrip_Utf8Body_PreservesData()
    {
        var original = new RconPacket(1, RconPacketType.ExecCommand, "café ☕ 日本語");
        var bytes = original.ToBytes();

        var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        var deserialized = RconPacket.FromPayload(bytes.AsSpan(4, size));

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void PacketType_EnumValues_MatchRconProtocol()
    {
        Assert.Equal(0, (int)RconPacketType.ResponseValue);
        Assert.Equal(2, (int)RconPacketType.ExecCommand);
        Assert.Equal(3, (int)RconPacketType.Auth);
    }

    [Fact]
    public void ToBytes_SizeField_EqualsPayloadLength()
    {
        var packet = new RconPacket(1, RconPacketType.Auth, "mypassword");
        var bytes = packet.ToBytes();

        var declaredSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        var actualPayloadLength = bytes.Length - 4; // total minus the size field itself

        Assert.Equal(actualPayloadLength, declaredSize);
    }
}
