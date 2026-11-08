using System.Runtime.InteropServices;

namespace IntergalacticJobCenter.Native;

internal static class NativeMethods
{
    private const string Library = "ijc_native";

    [DllImport(Library, EntryPoint = "ijc_placement_fee_cents", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int PlacementFeeCents(int score, int tentacles);
}
