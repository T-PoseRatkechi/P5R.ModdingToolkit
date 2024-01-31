using BF.File.Emulator.Interfaces;
using CriFs.V2.Hook.Interfaces;
using P5R.ModdingToolkit.Battles;
using P5R.ModdingToolkit.Configuration;
using P5R.ModdingToolkit.System;
using P5R.ModdingToolkit.Template;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using System.Diagnostics;
using System.Drawing;

namespace P5R.ModdingToolkit;

public class Mod : ModBase
{
    private readonly IModLoader modLoader;
    private readonly IReloadedHooks? hooks;
    private readonly ILogger log;
    private readonly IMod owner;
    private readonly IModConfig modConfig;

    private Config config;
    private readonly ICriFsRedirectorApi criFsApi;
    private readonly IBfEmulator bfEmulator;

    private readonly string modDir;
    private readonly string eventBattleBf;
    private readonly string bindDir;

    private readonly Encounters encounters;
    private readonly CountHook count = new();
    private readonly List<CustomEventBattle> customBattles = new();

    public Mod(ModContext context)
    {
        this.modLoader = context.ModLoader;
        this.hooks = context.Hooks!;
        this.log = context.Logger;
        this.owner = context.Owner;
        this.config = context.Configuration;
        this.modConfig = context.ModConfig;

#if DEBUG
        Debugger.Launch();
#endif

        this.modLoader.GetController<IStartupScanner>().TryGetTarget(out var scanner);
        this.modLoader.GetController<ICriFsRedirectorApi>().TryGetTarget(out this.criFsApi!);
        this.modLoader.GetController<IBfEmulator>().TryGetTarget(out this.bfEmulator!);

        Log.Initialize("Modding Toolkit", this.log, Color.Cyan);
        Log.LogLevel = LogLevel.Debug;

        this.modDir = this.modLoader.GetDirectoryForModId(this.modConfig.ModId);
        this.bindDir = this.criFsApi.GenerateBindingDirectory(this.modDir);

        var dummyFlowFile = Path.Join(this.modDir, "resources", "battles", "CommonBattle.flow");
        this.CreateCustomBattle(dummyFlowFile);

        this.eventBattleBf = Path.Join(this.modDir, "resources", "battles", "EventBattle.bf");

        this.encounters = new(this.customBattles);
        this.encounters.Initialize(scanner!, this.hooks);
        this.modLoader.ModLoading += this.OnModLoading;
        this.modLoader.OnModLoaderInitialized += this.OnModLoaderInitialized;
    }

    private void OnModLoaderInitialized()
    {
        foreach (var battle in this.customBattles)
        {
            this.bfEmulator.TryCreateFromBf(this.eventBattleBf, battle.Route, battle.BindedFile);
        }
    }

    private void OnModLoading(IModV1 mod, IModConfigV1 config)
    {
        if (!config.ModDependencies.Contains(this.modConfig.ModId))
        {
            return;
        }

        var modDir = this.modLoader.GetDirectoryForModId(config.ModId);
        var toolkitDir = Path.Join(modDir, "toolkit");
        if (!Directory.Exists(toolkitDir))
        {
            return;
        }

        var battlesDir = Path.Join(toolkitDir, "battles");
        if (Directory.Exists(battlesDir))
        {
            foreach (var file in Directory.EnumerateFiles(battlesDir, "*.flow"))
            {
                this.CreateCustomBattle(file);
            }
        }
    }

    private void CreateCustomBattle(string battleFlowFile)
    {
        var route = Path.GetFileName(battleFlowFile);
        this.bfEmulator.AddFile(battleFlowFile, route);

        var encounterId = Path.GetFileNameWithoutExtension(battleFlowFile)
            .Equals("CommonBattle", StringComparison.OrdinalIgnoreCase)
            ? 0
            : int.Parse(Path.GetFileNameWithoutExtension(battleFlowFile));

        if (this.customBattles.Any(x => x.EncounterId == encounterId) == false)
        {
            var bindedBattleFile = Path.Join(this.bindDir, Path.ChangeExtension(route, ".bf"));
            this.criFsApi.AddBind(bindedBattleFile, $"BATTLE/EVENT/SCRIPT/{encounterId:X4}.bf", this.modConfig.ModId);
            this.customBattles.Add(new(encounterId, bindedBattleFile, route));

            Log.Information($"Registered event battle: {route} || {encounterId}");
        }
    }

    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        config = configuration;
        log.WriteLine($"[{modConfig.ModId}] Config Updated: Applying");
    }
    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}
