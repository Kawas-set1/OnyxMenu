Put the reference assemblies here (they are NOT distributed with the source).

Copy these DLLs from your BepInEx IL2CPP install / interop folder into this lib\ folder:

  Assembly-CSharp.dll
  Assembly-CSharp-firstpass.dll
  0Harmony.dll
  BepInEx.Core.dll
  BepInEx.Unity.IL2CPP.dll
  Hazel.dll
  Il2CppInterop.Runtime.dll
  Il2Cppmscorlib.dll
  Il2CppSystem.Core.dll
  Unity.TextMeshPro.dll
  Unity.ResourceManager.dll
  Unity.Addressables.dll
  UnityEngine.AudioModule.dll
  UnityEngine.CoreModule.dll
  UnityEngine.IMGUIModule.dll
  UnityEngine.ImageConversionModule.dll
  UnityEngine.InputLegacyModule.dll
  UnityEngine.Physics2DModule.dll
  UnityEngine.TextRenderingModule.dll
  UnityEngine.UI.dll
  UnityEngine.UnityAnalyticsModule.dll
  UnityEngine.CrashReportingModule.dll
  UnityEngine.PerformanceReportingModule.dll

Typical source: <AmongUs>\BepInEx\interop\  (Unity/Il2Cpp DLLs) and <AmongUs>\BepInEx\core\ (BepInEx/Harmony).

Alternatively, build without copying by pointing at your own folder:
  dotnet build -c Release -p:GameRefsDir="C:\path\to\your\lib\"

NAudio / NVorbis are pulled from NuGet automatically and embedded — no manual step.
