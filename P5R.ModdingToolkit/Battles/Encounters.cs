using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using System.Runtime.InteropServices;
using static Reloaded.Hooks.Definitions.X64.FunctionAttribute;

namespace P5R.ModdingToolkit.Battles;

internal unsafe class Encounters : IGameHook
{
    [Function(CallingConventions.Microsoft)]
    private delegate nint LoadEncounter(nint param1, nint encounterPtr);
    private IHook<LoadEncounter>? loadEncounterHook;

    [Function(CallingConventions.Microsoft)]
    private delegate nint LoadEventBattleScript(int encounterId, nint param2, nint param3, nint param4);
    private IHook<LoadEventBattleScript>? loadEventBattleScriptHook;

    [Function(CallingConventions.Microsoft)]
    private delegate byte FUN_140e29310(uint param1);
    private FUN_140e29310? FUN_140e29310_wrapper;

    [Function(Register.rcx, Register.rax, true)]
    private delegate bool IsEventBattle(nint ptr1);
    private IReverseWrapper<IsEventBattle>? eventBattleChecksWrapper;
    private IAsmHook? eventBattleChecksHook;

    private readonly bool* isCommonBattle = (bool*)Marshal.AllocHGlobal(sizeof(bool));

    public Action<int>? EncounterLoading;

    public Encounters()
    {
        *this.isCommonBattle = false;
    }

    public void Initialize(IStartupScanner scanner, IReloadedHooks hooks)
    {
        scanner.Scan("LoadEncounter Function", "48 8B C4 48 89 50 ?? 48 89 48 ?? 55 53 56 57 41 54 41 55 41 56 41 57 48 8D A8 ?? ?? ?? ?? 48 81 EC 08 07 00 00",
            result => this.loadEncounterHook = hooks.CreateHook<LoadEncounter>(this.LoadEncounterImpl, result).Activate());

        scanner.Scan("LoadEventBattleScript Function", "48 89 6C 24 ?? 48 89 74 24 ?? 41 56 48 81 EC 30 01 00 00",
            result => this.loadEventBattleScriptHook = hooks.CreateHook<LoadEventBattleScript>(this.LoadEventBattleScriptImpl, result).Activate());

        scanner.Scan("Event Encounter Checks Hook", "75 ?? B9 1C 01 00 30 E8 ?? ?? ?? ?? 84 C0 74 ?? 48 8D 95", result =>
        {
            var patch = new string[]
            {
                "use64",
                "mov rcx, r14",
                Utilities.PushCallerRegisters,
                hooks.Utilities.GetAbsoluteCallMnemonics(this.IsEventBattleImpl, out this.eventBattleChecksWrapper),
                Utilities.PopCallerRegisters,
                hooks.Utilities.GetAbsoluteJumpMnemonics(result + 0x10, true),
            };

            this.eventBattleChecksHook = hooks.CreateAsmHook(patch, result).Activate();
        });

        scanner.Scan("FUN_140e29310", "4C 8D 05 ?? ?? ?? ?? 33 C0 49 8B D0 0F 1F 40 00 39 0A 74 ?? FF C0 48 83 C2 08 83 F8 10 72 ?? 8B D1", result =>
        {
            this.FUN_140e29310_wrapper = hooks.CreateWrapper<FUN_140e29310>(result, out _);
        });
    }

    private nint LoadEventBattleScriptImpl(int encounterId, nint param2, nint param3, nint param4)
    {
        if (*this.isCommonBattle)
        {
            Log.Information("Using common encounter script.");
            return this.loadEventBattleScriptHook!.OriginalFunction(0, param2, param3, param4);
        }

        return this.loadEventBattleScriptHook!.OriginalFunction(encounterId, param2, param3, param4);
    }

    private nint LoadEncounterImpl(nint param1, nint encounterPtr)
    {
        var encounterId = GetEncounterId(encounterPtr);
        Log.Information($"Loading Encounter: {encounterId}");
        this.EncounterLoading?.Invoke(encounterId);
        return this.loadEncounterHook!.OriginalFunction(param1, encounterPtr);
    }

    private bool IsEventBattleImpl(nint ptr1)
    {
        if ((*(uint*)ptr1 & 0x120) != 0)
        {
            *this.isCommonBattle = false;
            Log.Information($"Event Battle Condition 1: true");
            return true;
        }

        if (this.FUN_140e29310_wrapper!(0x3000011c) != 0)
        {
            *this.isCommonBattle = false;
            Log.Information($"Event Battle Condition 2: true");
            return true;
        }

        *this.isCommonBattle = true;
        return false;
    }

    private static int GetEncounterId(nint encounterPtr)
    {
        var id = *(int*)(encounterPtr + 0x278);
        return id;
    }
}
