using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;

namespace P5R.ModdingToolkit.System;

internal class CountHook : IGameHook
{
    [Function(CallingConventions.Microsoft)]
    private delegate int GetCount(int id);
    private IHook<GetCount>? getCountHook;

    private int lastId = 0;

    public void Initialize(IStartupScanner scanner, IReloadedHooks hooks)
    {
        scanner.Scan("GET_COUNT", "4C 8D 05 ?? ?? ?? ?? 33 C0 49 8B D0 0F 1F 40 00 39 0A 74 ?? FF C0 48 83 C2 08 83 F8 02 72 ?? 48 63 C1 48 8D 0D ?? ?? ?? ?? 8B 84",
            result => this.getCountHook = hooks.CreateHook<GetCount>(this.GetCountImpl, result).Activate());
    }

    private int GetCountImpl(int id)
    {
        var result = this.getCountHook!.OriginalFunction(id);
        if (this.lastId != id)
        {
            Log.Debug($"GET_COUNT({id}): {result}");
            this.lastId = id;
        }

        return result;
    }
}
