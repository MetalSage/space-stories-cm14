using System.IO;
using Content.Shared._Stories.Hunter.Profiles;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences
{
    /// <summary>
    /// The server sends this before the client joins the lobby.
    /// </summary>
    [Serializable, NetSerializable] // Stories-Hunter
    public sealed class MsgPreferencesAndSettings : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;

        public PlayerPreferences Preferences = default!;
        public GameSettings Settings = default!;
        public HunterProfile? HunterProfile; // Stories-Hunter

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            var length = buffer.ReadVariableInt32();

            using (var stream = new MemoryStream())
            {
                buffer.ReadAlignedMemory(stream, length);
                stream.Position = 0; // Stories-Hunter
                serializer.DeserializeDirect(stream, out Preferences);
            }

            length = buffer.ReadVariableInt32();
            using (var stream = new MemoryStream())
            {
                buffer.ReadAlignedMemory(stream, length);
                stream.Position = 0; // Stories-Hunter
                serializer.DeserializeDirect(stream, out Settings);
            }

            // Stories-Hunter-Start
            if (buffer.ReadByte() == 1)
            {
                length = buffer.ReadVariableInt32();
                using var stream = new MemoryStream();
                buffer.ReadAlignedMemory(stream, length);
                stream.Position = 0;
                serializer.DeserializeDirect(stream, out HunterProfile);
            }
            // Stories-Hunter-End
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            using (var stream = new MemoryStream())
            {
                serializer.SerializeDirect(stream, Preferences);
                buffer.WriteVariableInt32((int) stream.Length);
                stream.TryGetBuffer(out var segment);
                buffer.Write(segment);
            }

            using (var stream = new MemoryStream())
            {
                serializer.SerializeDirect(stream, Settings);
                buffer.WriteVariableInt32((int) stream.Length);
                stream.TryGetBuffer(out var segment);
                buffer.Write(segment);
            }

            // Stories-Hunter-Start
            if (HunterProfile != null)
            {
                buffer.Write((byte)1);
                using var stream = new MemoryStream();
                serializer.SerializeDirect(stream, HunterProfile);
                buffer.WriteVariableInt32((int) stream.Length);
                stream.TryGetBuffer(out var segment);
                buffer.Write(segment);
            }
            else
            {
                buffer.Write((byte)0);
            }
            // Stories-Hunter-End
        }
    }
}
