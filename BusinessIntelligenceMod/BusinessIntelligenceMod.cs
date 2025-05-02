// Business Intelligence Mod for Schedule I
// Copyright (c) 2025 [Your Name]
// Licensed under the MIT License - see LICENSE file for details

using System.Reflection;
using BusinessIntelligenceMod.Utils;
using MelonLoader;

[assembly:
    MelonInfo(typeof(BusinessIntelligenceMod.BusinessIntelligenceMod), "BusinessIntelligenceMod", "1.0.0", "PirateSeal",
        null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BusinessIntelligenceMod;

public class BusinessIntelligenceMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("WeedAnalytics Mod Initializing...");

        Helpers.TryCatch.TryAsync(() =>
            {
                CsvLogger.Initialize();

                var harmony = new HarmonyLib.Harmony("com.pirateseal.weedanalytics");

                harmony.PatchAll(Assembly.GetExecutingAssembly());

                MelonLogger.Msg("Business Intelligence Mod initialized!");

                return Task.FromResult(Task.CompletedTask);
            },
            ex =>
            {
                MelonLogger.Error($"Error during initialization: {ex.Message}");
                return ex;
            }).Wait();
    }
}