using System;
using System.Linq;
using System.Text;

namespace AsarSharp
{
    class PickleIterator
    {
        public byte[] Payload { get; set; }
        public uint PayloadOffset { get; set; }
        public uint ReadIndex { get; set; }
        public uint EndIndex { get; set; }

        public PickleIterator(Pickle pickle)
        {
            Payload = pickle.Header;
            PayloadOffset = pickle.HeaderSize;
            ReadIndex = 0;
            EndIndex = pickle.GetPayloadSize();
        }

        public bool ReadBool()
        {
            return ReadInt() != 0;
        }

        public int ReadInt()
        {
            return ReadBytes(PickleUtils.SIZE_INT32, (readPayloadOffset) => BitConverter.ToInt32(Payload, (int)readPayloadOffset));
        }

        public uint ReadUInt32()
        {
            return ReadBytes(PickleUtils.SIZE_UINT32, (readPayloadOffset) => BitConverter.ToUInt32(Payload, (int)readPayloadOffset));
        }

        public long ReadInt64()
        {
            return ReadBytes(PickleUtils.SIZE_INT64, (readPayloadOffset) => BitConverter.ToInt64(Payload, (int)readPayloadOffset));
        }

        public int ReadUInt64()
        {
            return ReadBytes(PickleUtils.SIZE_UINT64, (readPayloadOffset) => BitConverter.ToUInt64(Payload, (int)readPayloadOffset));
        }

        public float ReadFloat()
        {
            return ReadBytes(PickleUtils.SIZE_FLOAT, (readPayloadOffset) => BitConverter.ToSingle(Payload, (int)readPayloadOffset));
        }

        public double ReadDouble()
        {
            return ReadBytes(PickleUtils.SIZE_DOUBLE, (readPayloadOffset) => BitConverter.ToDouble(Payload, (int)readPayloadOffset));
        }

        public string ReadString()
        {
            return ReadBytes<int>(ReadInt());
        }

        public dynamic ReadBytes<DelegateReturnType>(int length, Func<uint, DelegateReturnType> method = null)
        {
            return ReadBytes((uint)length, method);
        }

        public dynamic ReadBytes<DelegateReturnType>(uint length, Func<uint, DelegateReturnType> method = null)
        {

            uint readPayloadOffset = GetReadPayloadOffsetAndAdvance(length);
            if (method != null) return method(readPayloadOffset);

            return Encoding.UTF8.GetString(Payload.Skip((int)readPayloadOffset).Take((int)length).ToArray());
        }

        public uint GetReadPayloadOffsetAndAdvance(uint length)
        {
            if (length > EndIndex - ReadIndex)
            {
                ReadIndex = EndIndex;
                throw new Exception($"Failed to read data with length of {length}.");
            }

            uint readPayloadOffset = PayloadOffset + ReadIndex;
            Advance(length);
            return readPayloadOffset;
        }

        public void Advance(uint size)
        {
            uint alignedSize = PickleUtils.AlignInt(size, PickleUtils.SIZE_UINT32);
            if (EndIndex - ReadIndex < alignedSize)
                ReadIndex = EndIndex;
            else
                ReadIndex += alignedSize;
        }

    }

}
