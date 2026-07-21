using System;

namespace Onyx;

internal static class OnyxProfiler
{
    internal static bool Enabled => false;
    internal static IDisposable Sample(string name) => null;
    internal static void RecordAudioCallback(long startTicks) { }
}
