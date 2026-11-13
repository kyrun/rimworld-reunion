using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Kyrun.Reunion
{
    /// <summary>
    /// Compatibility fix for Surveillance Shift's Pawn.ExposeData postfix.
    /// Its postfix reads Find.FactionManager before checking whether Scribe is
    /// loading pawn data. Find.FactionManager dereferences Find.World, which is
    /// not initialized while Reunion's deep-saved pawns are being loaded.
    /// </summary>
    static class SurveillanceShiftCompatibility
    {
        const string MOD_PACKAGE_ID = "ferny.SurveillanceShift";
        const string PATCH_TYPE_NAME = "SurveillanceShift.Pawn_ExposeData_Patch";

        public static void Apply()
        {
            if (ModLister.GetActiveModWithIdentifier(MOD_PACKAGE_ID, true) == null)
            {
                return;
            }

            var patchType = AccessTools.TypeByName(PATCH_TYPE_NAME);
            var targetMethod = patchType == null
                ? null
                : AccessTools.Method(patchType, "Postfix", new[] { typeof(Pawn) });

            if (targetMethod == null)
            {
                Util.Warn("Surveillance Shift is active, but its Pawn_ExposeData_Patch.Postfix method could not be found. The save/load compatibility fix was not applied.");
                return;
            }

            var transpiler = AccessTools.Method(
                typeof(SurveillanceShiftCompatibility),
                nameof(Transpiler));

            try
            {
                Main.HarmonyInstance.Patch(
                    targetMethod,
                    transpiler: new HarmonyMethod(transpiler));

                Util.Msg("Installed the Surveillance Shift pawn-loading compatibility transpiler.");
            }
            catch (System.Exception ex)
            {
                Util.Error("Failed to apply the Surveillance Shift save/load compatibility patch: " + ex);
            }
        }

        // Preserve the original getter behaviour in every Scribe mode except
        // LoadingVars, where Find.World may not exist yet.
        static FactionManager GetFactionManagerSafely()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                return null;
            }

            return Find.FactionManager;
        }

        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var originalGetter = AccessTools.PropertyGetter(
                typeof(Find),
                nameof(Find.FactionManager));
            var replacementGetter = AccessTools.Method(
                typeof(SurveillanceShiftCompatibility),
                nameof(GetFactionManagerSafely));

            if (originalGetter == null || replacementGetter == null)
            {
                Util.Error("Failed to patch Surveillance Shift: could not resolve the faction-manager getter or its replacement.");
                return codes;
            }

            var matchingIndices = new List<int>();

            for (int i = 0; i < codes.Count; i++)
            {
                var instruction = codes[i];
                if ((instruction.opcode == OpCodes.Call ||
                     instruction.opcode == OpCodes.Callvirt) &&
                    Equals(instruction.operand, originalGetter))
                {
                    matchingIndices.Add(i);
                }
            }

            if (matchingIndices.Count != 1)
            {
                Util.Error(
                    "Failed to patch Surveillance Shift Pawn_ExposeData_Patch.Postfix: " +
                    "expected exactly one Find.FactionManager getter call, found " +
                    matchingIndices.Count + ". The original instructions were retained.");
                return codes;
            }

            codes[matchingIndices[0]].operand = replacementGetter;
            return codes;
        }
    }
}
