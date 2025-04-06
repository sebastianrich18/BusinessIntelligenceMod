using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;

namespace BusinessIntelligenceMod
{
    public class BusinessIntelligenceMod : MelonMod
    {
        public override void OnApplicationStart()
        {
            MelonLogger.Msg("Simple Test Mod successfully loaded!");

            // Create a folder for future use
            string modDataPath = System.IO.Path.Combine(
                UnityEngine.Application.persistentDataPath,
                "TestModData");

            try
            {
                if (!System.IO.Directory.Exists(modDataPath))
                {
                    System.IO.Directory.CreateDirectory(modDataPath);
                    MelonLogger.Msg($"Created directory at: {modDataPath}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to create directory: {ex.Message}");
            }
        }

        public override void OnUpdate()
        {
            // This runs every frame - we're not doing anything here for the test
        }
    }
}
