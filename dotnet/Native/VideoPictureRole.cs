namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum VideoPictureRole : byte
{
    /// <summary>distinct or output-only destination</summary>
    DECODE_OUTPUT,
    /// <summary>existing DPB picture read by the decode operation</summary>
    DECODE_REFERENCE,
    /// <summary>distinct reconstructed picture written into the DPB</summary>
    DECODE_SETUP,
    /// <summary>coincident destination and reconstructed DPB picture; incompatible with "VIDEO_OUTPUT_ONLY"</summary>
    DECODE_OUTPUT_AND_SETUP,
    /// <summary>source picture read by the encode operation</summary>
    ENCODE_INPUT,
    /// <summary>existing DPB picture read by the encode operation</summary>
    ENCODE_REFERENCE,
    /// <summary>reconstructed picture written into the DPB</summary>
    ENCODE_RECONSTRUCTED,
    MAX_NUM,
}
