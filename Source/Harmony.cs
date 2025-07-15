using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld.Planet;
using UnityEngine;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RW_MassAffect
{
    [StaticConstructorOnStartup]
    internal class HarmonyPatches
    {
        public static Type patchtype = typeof(HarmonyPatches);
        public static string patchname = "RW_MassAffect.HarmonyPatch";

       
        static HarmonyPatches()
        {
            // This static constructor is used to ensure that the HarmonyPatch class is loaded
            // and the patch is applied when the assembly is loaded.

            // Need to patch the private TicksPerMove in the Pawn class
            // to allow for faster movement speed.
            var harmony = new HarmonyLib.Harmony(patchname);

            try
            {
                
                harmony.Patch(
                    AccessTools.Method(typeof(Verse.Pawn), "TicksPerMove"),
                    prefix: new HarmonyMethod(typeof(HarmonyPatches.PawnPatch), nameof(PawnPatch.Prefix))
);
                Log.Message("MassAffect :: Harmony patch for TicksPerMove applied successfully.");
                harmony.PatchAll();
                Log.Message("MassAffect :: Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error applying Harmony patches: {ex.Message}");
            }
        }

        public static class PawnPatch
        {
            public static bool Prefix(Pawn __instance, ref float __result, bool diagonal)
            {
                float num = __instance.GetStatValue(StatDefOf.MoveSpeed);

                if (__instance.health.Downed && __instance.health.CanCrawl)
                {
                    num = __instance.GetStatValue(StatDefOf.CrawlSpeed);
                }
                if (RestraintsUtility.InRestraints(__instance))
                {
                    num *= 0.35f;
                }

                // Check carried pawn mass and adjust speed accordingly
                if (__instance.carryTracker?.CarriedThing != null && __instance.carryTracker.CarriedThing.def.category == ThingCategory.Pawn)
                {
                    // num *= 0.6f;
                    Pawn carriedPawn = __instance.carryTracker.CarriedThing as Pawn;

                    float carriedPawnMass = carriedPawn.GetStatValue(StatDefOf.Mass);
                    float carriedPawnGearMass = carriedPawn.apparel?.WornApparel.Sum(apparel => apparel.GetStatValue(StatDefOf.Mass)) ?? 0f;
                    float totalPawnMass = Mathf.Clamp(carriedPawnMass + carriedPawnGearMass, 0.01f, 1f);

                    //Log.Message($"MassAffect :: {__instance.Name} is carrying {carriedPawn.Name} with mass {carriedPawnMass}");
                    num *= Mathf.Clamp(1f - carriedPawnMass / __instance.GetStatValue(StatDefOf.CarryingCapacity), 0.01f, 1f);
                    //Log.Message($"MassAffect :: {__instance.Name} new move speed after carrying pawn adjustment: {num}");
                }

                // If Pawn is wearing gear, get mass and adjust speed accordingly
                if (__instance.apparel != null)
                {
                    float totalMass = __instance.apparel.WornApparel.Sum(apparel => apparel.GetStatValue(StatDefOf.Mass));
                    if (totalMass > 0f)
                    {
                        //Log.Message($"MassAffect :: {__instance.Name} is wearing gear with total mass {totalMass}");
                        num *= Mathf.Clamp(1f - totalMass / __instance.GetStatValue(StatDefOf.CarryingCapacity), 0.01f, 1f);
                        //Log.Message($"MassAffect :: {__instance.Name} new move speed after gear mass adjustment: {num}");
                    }
                }

                // If Pawn is carrying a thing, get its mass and adjust speed accordingly
                if (__instance.carryTracker?.CarriedThing != null && __instance.carryTracker.CarriedThing.def.BaseMass > 0f)
                {
                    //Log.Message($"MassAffect :: {__instance.Name} is carrying {__instance.carryTracker.CarriedThing.Label} with mass {__instance.carryTracker.CarriedThing.GetStatValue(StatDefOf.Mass)}");
                    num *= Mathf.Clamp(1f - __instance.carryTracker.CarriedThing.GetStatValue(StatDefOf.Mass) / __instance.GetStatValue(StatDefOf.CarryingCapacity), 0.01f, 1f);
                    //Log.Message($"MassAffect :: {__instance.Name} new move speed after carrying mass adjustment: {num}");
                }
                
                float num2 = num / 60f;
                float num3;
                if (num2 == 0f)
                {
                    num3 = 450f;
                }
                else
                {
                    num3 = 1f / num2;
                    if (__instance.Spawned && !__instance.Map.roofGrid.Roofed(__instance.Position))
                    {
                        num3 /= __instance.Map.weatherManager.CurMoveSpeedMultiplier;
                    }
                    if (diagonal)
                    {
                        num3 *= 1.41421f;
                    }
                }
                num3 = Mathf.Clamp(num3, 1f, 450f);
                if (__instance.debugMaxMoveSpeed)
                {
                    __result = 1f;
                }
                else
                {
                    __result = num3;
                }
                return false; // Skip original method
            }
        }
    }
}
