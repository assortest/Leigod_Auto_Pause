
namespace AsarSharp
{
 class PickleUtils
    {
        // sizeof(T).
        public const ushort SIZE_INT32 = 4;
        public const ushort SIZE_UINT32 = 4;
        public const ushort SIZE_INT64 = 8;
        public const ushort SIZE_UINT64 = 8;
        public const ushort SIZE_FLOAT = 4;
        public const ushort SIZE_DOUBLE = 8;

        // The allocation granularity of the payload.
        public const ushort PAYLOAD_UNIT = 64;

        // Largest JS number.
        public const ulong CAPACITY_READ_ONLY = 9007199254740992;

        // Aligns 'number' by rounding it up to the next multiple of 'alignment'.
        public static uint AlignInt(uint number, uint alignment)
        {
            return (number + alignment - 1) & ~(alignment - 1);
        }

    }
}
