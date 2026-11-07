namespace SharpNRI.Native;

/// <summary>Expects "__ANDROID__" platform macro</summary>
public unsafe partial struct AndroidWindow
{
    /// <summary>ANativeWindow</summary>
    public void* nativeWindow;
}
