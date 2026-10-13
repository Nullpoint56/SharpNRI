using System.Runtime.CompilerServices;

namespace PrismNRI.Native;

public partial struct TopLevelInstance
{
    [NativeTypeName("float[3][4]")]
    public _transform_e__FixedBuffer transform;

    public uint _bitfield1;

    [NativeTypeName("uint32_t : 24")]
    public uint instanceId
    {
        readonly get
        {
            return _bitfield1 & 0xFFFFFFu;
        }

        set
        {
            _bitfield1 = (_bitfield1 & ~0xFFFFFFu) | (value & 0xFFFFFFu);
        }
    }

    [NativeTypeName("uint32_t : 8")]
    public uint mask
    {
        readonly get
        {
            return (_bitfield1 >> 24) & 0xFFu;
        }

        set
        {
            _bitfield1 = (_bitfield1 & ~(0xFFu << 24)) | ((value & 0xFFu) << 24);
        }
    }

    public uint _bitfield2;

    [NativeTypeName("uint32_t : 24")]
    public uint shaderBindingTableRecordOffset
    {
        readonly get
        {
            return _bitfield2 & 0xFFFFFFu;
        }

        set
        {
            _bitfield2 = (_bitfield2 & ~0xFFFFFFu) | (value & 0xFFFFFFu);
        }
    }

    [NativeTypeName("nri::TopLevelInstanceBits : 8")]
    public TopLevelInstanceBits flags
    {
        readonly get
        {
            return (TopLevelInstanceBits)((_bitfield2 >> 24) & 0xFFu);
        }

        set
        {
            _bitfield2 = (_bitfield2 & ~(0xFFu << 24)) | (((uint)(value) & 0xFFu) << 24);
        }
    }

    [NativeTypeName("uint64_t")]
    public ulong accelerationStructureHandle;

    [InlineArray(3 * 4)]
    public partial struct _transform_e__FixedBuffer
    {
        public float e0_0;
    }
}
