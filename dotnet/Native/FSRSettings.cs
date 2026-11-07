namespace SharpNRI.Native;

public partial struct FSRSettings
{
    /// <summary>distance to the near plane (units)</summary>
    public float zNear;

    /// <summary>distance to the far plane, unused if "DEPTH_INFINITE" is set (units)</summary>
    public float zFar;

    /// <summary>vertical field of view angle (radians)</summary>
    public float verticalFov;

    /// <summary>the time elapsed since the last frame (ms)</summary>
    public float frameTime;

    /// <summary>for converting view space units to meters (m/unit)</summary>
    public float viewSpaceToMetersFactor;

    /// <summary>[0; 1]</summary>
    public float sharpness;
}
