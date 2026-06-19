using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace RW_MassAffect
{
    [StaticConstructorOnStartup]
    internal class HarmonyPatches
    {
        public static Type patchtype = typeof(HarmonyPatches);
        public static string patchname = "RW_MassAffect.HarmonyPatch";

        private static float pawnSpeedModifier = 1f;
        private const float MinMassFactor = 0.10f;
        private const int MaxAssistHelpers = 2;
        private const float AssistCapacityBonusPerHelper = 0.25f;
        private const string AssistJobDefName = "MA_CarryAssistance";

        public static float PawnSpeedModifier
        {
            get
            {
                return pawnSpeedModifier;
            }
            set
            {
                if (value < 0.01f || value > 10f)
                {
                    Log.Warning($"MassAffect :: PawnSpeedModifier set to {value}, which is out of bounds. Clamping to 1f.");
                    value = 1f;
                }

                pawnSpeedModifier = value;
            }
        }

        static HarmonyPatches()
        {
            var harmony = new HarmonyLib.Harmony(patchname);

            try
            {
                harmony.Patch(
                    AccessTools.Method(typeof(Pawn), "TicksPerMove"),
                    postfix: new HarmonyMethod(typeof(PawnPatch), nameof(PawnPatch.Postfix))
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
            public static void Postfix(Pawn __instance, ref float __result)
            {
                if (__instance == null || __result <= 0f)
                {
                    return;
                }

                float massFactor = ComputeMassMoveFactor(__instance);
                if (Mathf.Approximately(massFactor, 1f) && Mathf.Approximately(PawnSpeedModifier, 1f))
                {
                    return;
                }

                float totalFactor = Mathf.Clamp(massFactor * PawnSpeedModifier, MinMassFactor, 10f);
                if (!Mathf.Approximately(totalFactor, 1f))
                {
                    __result = Mathf.Clamp(__result / totalFactor, 1f, 450f);
                }
            }

            private static float ComputeMassMoveFactor(Pawn pawn)
            {
                float carryingCapacity = Mathf.Max(1f, pawn.GetStatValue(StatDefOf.CarryingCapacity));
                float carriedMass = 0f;

                Thing burden = pawn.carryTracker?.CarriedThing;
                if (burden != null && burden != pawn)
                {
                    carriedMass += Mathf.Max(0f, burden.GetStatValue(StatDefOf.Mass));

                    if (burden is Pawn carriedPawn && carriedPawn.apparel?.WornApparel != null)
                    {
                        carriedMass += carriedPawn.apparel.WornApparel.Sum(apparel => Mathf.Max(0f, apparel.GetStatValue(StatDefOf.Mass)));
                    }
                }

                float helperBonusFactor = 1f;
                if (pawn.CurJobDef == JobDefOf.HaulToCell && burden is Corpse)
                {
                    int helperCount = pawn.Map?.mapPawns?.SpawnedPawnsInFaction(Faction.OfPlayer)
                        .Count(p => p != pawn
                            && p.CurJobDef != null
                            && p.CurJobDef.defName == AssistJobDefName
                            && p.CurJob?.targetA.Thing == pawn
                            && p.Position.InHorDistOf(pawn.Position, 2.9f)) ?? 0;

                    helperCount = Mathf.Clamp(helperCount, 0, MaxAssistHelpers);
                    helperBonusFactor += helperCount * AssistCapacityBonusPerHelper;
                }

                float effectiveCapacity = carryingCapacity * helperBonusFactor;
                if (effectiveCapacity <= 0.01f)
                {
                    return MinMassFactor;
                }

                return Mathf.Clamp(1f - (carriedMass / effectiveCapacity), MinMassFactor, 1f);
            }
        }
    }
}
