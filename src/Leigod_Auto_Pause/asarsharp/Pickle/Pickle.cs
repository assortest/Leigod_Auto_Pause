// THIS IS A C# PICKLE PORT FROM JAVASCRIPT. ALL RIGHTS GO TO THE CHROMIUM AUTHORS.
// https://github.com/electron/node-chromium-pickle-js

// ********************************************************************************

using System;
using System.Text;
using System.Linq;
using System.Buffers;

namespace AsarSharp
{
    // This class provides facilities for basic binary value packing and unpacking.
    //
    // The Pickle class supports appending primitive values (ints, strings, etc.)
    // to a pickle instance. The Pickle instance grows its internal memory buffer
    // dynamically to hold the sequence of primitive values. The internal memory
    // buffer is exposed as the "data" of the Pickle. This "data" can be passed
    // to a Pickle object to initialize it for reading.
    //
    // When reading from a Pickle object, it is important for the consumer to know
    // what value types to read and in what order to read them as the Pickle does
    // not keep track of the type of data written to it.
    //
    // The Pickle's data has a header which contains the size of the Pickle's
    // payload. It can optionally support additional space in the header. That
    // space is controlled by the header_size parameter passed to the Pickle
    // constructor.
    class Pickle
    {
        public byte[] Header { get; set; }
        public uint HeaderSize { get; set; }
        public ulong CapacityAfterHeader { get; set; }
        public uint WriteOffset { get; set; }

        public Pickle()
        {

            Header = Array.Empty<byte>();
            HeaderSize = PickleUtils.SIZE_UINT32;
            CapacityAfterHeader = 0;
            WriteOffset = 0;

            Resize(PickleUtils.PAYLOAD_UNIT);
            SetPayloadSize(0);
        }

        public Pickle(byte[] buffer)
        {

            Header = buffer;
            HeaderSize = (uint)(buffer.Length - GetPayloadSize());
            CapacityAfterHeader = PickleUtils.CAPACITY_READ_ONLY;
            WriteOffset = 0;

            if (HeaderSize > buffer.Length) HeaderSize = 0;
            if (HeaderSize != PickleUtils.AlignInt(HeaderSize, PickleUtils.SIZE_UINT32)) HeaderSize = 0;

            if (HeaderSize == 0) Header = Array.Empty<byte>();

        }

        private void WriteMethod(dynamic data, uint writeOffset)
        {
            if (!data.GetType().Equals(typeof(string)))
                BitConverter.GetBytes(data).CopyTo(Header, writeOffset);
            else
            {
                byte[] dataStringBytes = Encoding.UTF8.GetBytes(data);
                Buffer.BlockCopy(src: dataStringBytes, srcOffset: 0, dst: Header, dstOffset: (int)writeOffset, count: dataStringBytes.Length);
            }
        }

        public PickleIterator CreateIterator()
        {
            return new PickleIterator(this);
        }

        public byte[] ToBuffer()
        {
            return Header.Take((int)(HeaderSize + GetPayloadSize())).ToArray();
        }

        public bool WriteBool(bool value)
        {
            return WriteInt(value ? 1 : 0);
        }

        public bool WriteInt(int value)
        {
            return WriteBytes(value, PickleUtils.SIZE_INT32, WriteMethod);
        }
        

        public bool WriteUInt32(uint value)
        {
            return WriteBytes(value, PickleUtils.SIZE_UINT32, WriteMethod);
        }


        public bool WriteInt64(long value)
        {
            return WriteBytes(value, PickleUtils.SIZE_UINT64, WriteMethod);
        }

        public bool WriteUInt64(ulong value)
        {
            return WriteBytes(value, PickleUtils.SIZE_UINT64, WriteMethod);
        }

        public bool WriteFloat(float value)
        {
            return WriteBytes(value, PickleUtils.SIZE_FLOAT, WriteMethod);
        }

        public bool WriteDouble(double value)
        {
            return WriteBytes(value, PickleUtils.SIZE_DOUBLE, WriteMethod);
        }

        public bool WriteString(string value)
        {
            int byteLength = Encoding.UTF8.GetBytes(value).Length;

            bool wroteSuccessfully = WriteInt(byteLength);
            if (!wroteSuccessfully) return false;

            return WriteBytes(value, (uint)byteLength);
        }

        public uint GetPayloadSize()
        {
            return BitConverter.ToUInt32(Header, startIndex: 0);
        }

        public void SetPayloadSize(uint newSize)
        {
            byte[] newSizeBytes = BitConverter.GetBytes(newSize);
            Buffer.BlockCopy(src: newSizeBytes, srcOffset: 0, dst: Header, dstOffset: 0, count: newSizeBytes.Length); 
        }

        public bool WriteBytes(dynamic data, uint length, Action<dynamic, uint> writeMethod = null)
        {
            uint dataLength = PickleUtils.AlignInt(length, PickleUtils.SIZE_UINT32);
            uint newSize = WriteOffset + dataLength;
            uint writeOffset = HeaderSize + WriteOffset;
            uint endOffset = HeaderSize + WriteOffset + length;

            if (newSize > CapacityAfterHeader)
                Resize((uint)Math.Max(CapacityAfterHeader * 2, newSize));

            if (writeMethod != null) writeMethod(data, writeOffset);
            else
            {
                // Buffer.BlockCopy doesn't accept strings, only char[]s
                if (data.GetType().Equals(typeof(string)))
                {
                    byte[] stringBytes = Encoding.UTF8.GetBytes(data);
                    Buffer.BlockCopy(src: stringBytes, srcOffset: 0, dst: Header, dstOffset: (int)writeOffset, count: (int)length);
                }
                else
                    Buffer.BlockCopy(src: data, srcOffset: 0, dst: Header, dstOffset: (int)writeOffset, count: (int)length);
            }
            
            Array.Fill<byte>(Header, value: 0, startIndex: (int)endOffset, count: (int)(dataLength - length));

            SetPayloadSize(newSize);
            WriteOffset = newSize;

            return true;
        }

        public void Resize(uint newCapacity)
        {
            newCapacity = PickleUtils.AlignInt(newCapacity, PickleUtils.PAYLOAD_UNIT);

            Header = Header.Concat(second: new byte[newCapacity]).ToArray();
            CapacityAfterHeader = newCapacity;
        }
    }
}
