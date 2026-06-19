using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
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
        private const float MaxMassFactor = 1.35f;
        private const string AssistJobDefName = "MA_CarryAssistance";
        private const bool DebugMassAffect = true;
        private const int DebugLogIntervalTicks = 120;
        private const float HelperContributionRange = 4.5f;

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

                harmony.Patch(
                    AccessTools.Method(typeof(MassUtility), "GearAndInventoryMass", new[] { typeof(Pawn) }),
                    postfix: new HarmonyMethod(typeof(MassPatch), nameof(MassPatch.Postfix))
                );

                harmony.Patch(
                    AccessTools.Method(typeof(StatWorker), "GetValueUnfinalized"),
                    postfix: new HarmonyMethod(typeof(MoveSpeedStatPatch), nameof(MoveSpeedStatPatch.Postfix))
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

                float carriedMass;
                float effectiveCapacity;
                float helperCapacity;
                int helperCount;
                float massFactor = ComputeMassMoveFactor(__instance, out carriedMass, out effectiveCapacity, out helperCapacity, out helperCount);
                if (Mathf.Approximately(massFactor, 1f) && Mathf.Approximately(PawnSpeedModifier, 1f))
                {
                    return;
                }

                float totalFactor = Mathf.Clamp(massFactor * PawnSpeedModifier, MinMassFactor, 10f);
                if (!Mathf.Approximately(totalFactor, 1f))
                {
                    __result = Mathf.Clamp(__result / totalFactor, 1f, 450f);
                }

                if (DebugMassAffect
                    && __instance.IsHashIntervalTick(DebugLogIntervalTicks)
                    && __instance.carryTracker?.CarriedThing != null)
                {
                    Log.Message(
                        $"MassAffect Debug :: pawn={__instance.LabelShortCap} " +
                        $"carrying={__instance.carryTracker.CarriedThing.LabelCap} " +
                        $"carriedMass={carriedMass:F2} capacity={effectiveCapacity:F2} helperCapacity={helperCapacity:F2} " +
                        $"helpers={helperCount} massFactor={massFactor:F3} totalFactor={totalFactor:F3} ticksPerMove={__result:F2}"
                    );
                }
            }

            private static float ComputeMassMoveFactor(Pawn pawn)
            {
                float carriedMass;
                float effectiveCapacity;
                float helperCapacity;
                int helperCount;
                return ComputeMassMoveFactor(pawn, out carriedMass, out effectiveCapacity, out helperCapacity, out helperCount);
            }

            public static float GetMassFactor(Pawn pawn, out float carriedMass, out float effectiveCapacity)
            {
                float helperCapacity;
                int helperCount;
                return ComputeMassMoveFactor(pawn, out carriedMass, out effectiveCapacity, out helperCapacity, out helperCount);
            }

            public static List<Pawn> GetActiveHelpersForCarrier(Pawn carrier)
            {
                if (carrier?.Map == null || Faction.OfPlayer == null)
                {
                    return new List<Pawn>();
                }

                return carrier.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                    .Where(p => p != carrier
                        && p.Spawned
                        && !p.Downed
                        && p.CurJobDef != null
                        && p.CurJobDef.defName == AssistJobDefName
                        && p.Position.InHorDistOf(carrier.Position, HelperContributionRange)
                        && p.CurJob?.targetA.Thing == carrier)
                    .ToList();
            }

            private static float ComputeMassMoveFactor(
                Pawn pawn,
                out float carriedMass,
                out float effectiveCapacity,
                out float helperCapacity,
                out int helperCount)
            {
                float carryingCapacity = Mathf.Max(1f, pawn.GetStatValue(StatDefOf.CarryingCapacity));
                carriedMass = 0f;
                helperCapacity = 0f;
                helperCount = 0;

                Thing burden = pawn.carryTracker?.CarriedThing;
                if (burden != null && burden != pawn)
                {
                    carriedMass += HarmonyPatches.GetThingTotalMass(burden);

                    if (burden is Pawn carriedPawn && carriedPawn.apparel?.WornApparel != null)
                    {
                        carriedMass += carriedPawn.apparel.WornApparel.Sum(apparel => Mathf.Max(0f, apparel.GetStatValue(StatDefOf.Mass)));
                    }

                    List<Pawn> helpers = GetActiveHelpersForCarrier(pawn);
                    helperCount = helpers.Count;
                    helperCapacity = helpers.Sum(helper =>
                    {
                        float maxCap = Mathf.Max(0f, helper.GetStatValue(StatDefOf.CarryingCapacity));
                        float carriedMass = helper.carryTracker?.CarriedThing != null
                            ? HarmonyPatches.GetThingTotalMass(helper.carryTracker.CarriedThing)
                            : 0f;
                        return Mathf.Max(0f, maxCap - carriedMass);
                    });
                }

                effectiveCapacity = carryingCapacity + helperCapacity;
                if (effectiveCapacity <= 0.01f)
                {
                    return MinMassFactor;
                }

                if (carriedMass <= 0.01f)
                {
                    return 1f;
                }

                // Team carrying model: speed factor scales with effective team carry capacity vs current load.
                // Cap the max boost so the carrier doesn't visibly sprint away from assistants.
                return Mathf.Clamp(effectiveCapacity / carriedMass, MinMassFactor, MaxMassFactor);
            }
        }

        public static class MoveSpeedStatPatch
        {
            private static readonly System.Reflection.FieldInfo StatField = AccessTools.Field(typeof(StatWorker), "stat");

            public static void Postfix(StatWorker __instance, StatRequest req, ref float __result)
            {
                if (StatField?.GetValue(__instance) is not StatDef statDef
                    || statDef != StatDefOf.MoveSpeed
                    || !req.HasThing
                    || req.Thing is not Pawn pawn)
                {
                    return;
                }

                float carriedMass;
                float effectiveCapacity;
                float massFactor = PawnPatch.GetMassFactor(pawn, out carriedMass, out effectiveCapacity);
                float totalFactor = Mathf.Clamp(massFactor * PawnSpeedModifier, MinMassFactor, 10f);

                if (!Mathf.Approximately(totalFactor, 1f))
                {
                    __result *= totalFactor;
                }
            }
        }

        public static class MassPatch
        {
            public static void Postfix(Pawn pawn, ref float __result)
            {
                if (pawn == null)
                {
                    return;
                }

                Thing burden = pawn.carryTracker?.CarriedThing;
                if (burden == null || burden == pawn)
                {
                    return;
                }

                __result += HarmonyPatches.GetThingTotalMass(burden);
            }
        }

        private static float GetThingTotalMass(Thing thing)
        {
            if (thing == null)
            {
                return 0f;
            }

            float unitMass = Mathf.Max(0f, thing.GetStatValue(StatDefOf.Mass));
            int stackCount = Mathf.Max(1, thing.stackCount);
            return unitMass * stackCount;
        }
    }
}
