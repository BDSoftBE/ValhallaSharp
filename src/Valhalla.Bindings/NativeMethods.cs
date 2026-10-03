using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Valhalla.Bindings;

internal static partial class NativeMethods
{
    private const string Library = "valhalla_sharp";

    [LibraryImport(Library, EntryPoint = "vs_actor_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Create(string config, out nint actor, out nint error);

    [LibraryImport(Library, EntryPoint = "vs_actor_request", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Request(ActorHandle actor, int action, string request, out nint result, out nint error);

    [LibraryImport(Library, EntryPoint = "vs_actor_destroy")]
    internal static partial void Destroy(nint actor);

    [LibraryImport(Library, EntryPoint = "vs_string_free")]
    internal static partial void Free(nint value);

    internal static string TakeString(nint value)
    {
        try { return Marshal.PtrToStringUTF8(value) ?? string.Empty; }
        finally { if (value != 0) Free(value); }
    }
}

internal sealed class ActorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal ActorHandle(nint value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle()
    {
        NativeMethods.Destroy(handle);
        return true;
    }
}
