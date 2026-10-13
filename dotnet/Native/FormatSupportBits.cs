using System;
namespace PrismNRI.Native;

[NativeTypeName("uint32_t")]
[Flags]
public enum FormatSupportBits : uint
{
    UNSUPPORTED = 0,
    TEXTURE = (1U << (0)),
    STORAGE_TEXTURE = (1U << (1)),
    STORAGE_TEXTURE_ATOMICS = (1U << (2)),
    COLOR_ATTACHMENT = (1U << (3)),
    DEPTH_STENCIL_ATTACHMENT = (1U << (4)),
    BLEND = (1U << (5)),
    MULTISAMPLE_2X = (1U << (6)),
    MULTISAMPLE_4X = (1U << (7)),
    MULTISAMPLE_8X = (1U << (8)),
    MULTISAMPLE_RESOLVE = (1U << (9)),
    BUFFER = (1U << (10)),
    STORAGE_BUFFER = (1U << (11)),
    STORAGE_BUFFER_ATOMICS = (1U << (12)),
    VERTEX_BUFFER = (1U << (13)),
    STORAGE_READ_WITHOUT_FORMAT = (1U << (14)),
    STORAGE_WRITE_WITHOUT_FORMAT = (1U << (15)),
    HOST_COPY = (1U << (16)),
}
