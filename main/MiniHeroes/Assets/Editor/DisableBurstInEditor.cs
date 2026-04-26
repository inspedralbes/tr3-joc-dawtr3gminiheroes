#if UNITY_EDITOR
using Unity.Burst;
using UnityEditor;

[InitializeOnLoad]
public static class DisableBurstInEditor
{
    static DisableBurstInEditor()
    {
        // Prevent Burst JIT from loading generated native DLLs in Editor sessions.
        BurstCompiler.Options.EnableBurstCompilation = false;
    }
}
#endif
