using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RW_MassAffect.defs
{
    public class WorkGiver_CarryAssistance : WorkGiver_Scanner
    {
        private readonly JobChecks jobChecks = new JobChecks();

        private const float OverloadThreshold = 0.75f;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            if (t.Thing is not Pawn carrier)
            {
                return 0f;
            }

            if (carrier?.carryTracker?.CarriedThing == null)
            {
                return 0f;
            }

            float capacity = Mathf.Max(1f, carrier.GetStatValue(StatDefOf.CarryingCapacity));
            float carriedMass = carrier.carryTracker.CarriedThing.GetStatValue(StatDefOf.Mass)
                * Mathf.Max(1, carrier.carryTracker.CarriedThing.stackCount);
            
            float utilizationRatio = Mathf.Clamp01(carriedMass / capacity);
            float remainingPercent = 1f - utilizationRatio;
            
            // Scale priority inversely with remaining capacity: <10% remaining = very high priority
            if (remainingPercent < 0.1f)
            {
                return 50f;
            }
            if (remainingPercent < 0.25f)
            {
                return 20f;
            }
            if (remainingPercent < 0.5f)
            {
                return 10f;
            }
            
            return 1f;
        }

        private static bool IsCarrierOverloaded(Pawn carrier)
        {
            if (carrier?.carryTracker?.CarriedThing == null)
            {
                return false;
            }

            float capacity = Mathf.Max(1f, carrier.GetStatValue(StatDefOf.CarryingCapacity));
            float carriedMass = carrier.carryTracker.CarriedThing.GetStatValue(StatDefOf.Mass)
                * Mathf.Max(1, carrier.carryTracker.CarriedThing.stackCount);

            return (carriedMass / capacity) >= OverloadThreshold;
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                yield break;
            }

            foreach (Pawn carrier in jobChecks.ActiveCarriersNeedingHelp(pawn.Map, pawn))
            {
                yield return carrier;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (pawn?.Map == null)
            {
                return true;
            }

            // Skip if pawn is already assisting someone
            if (pawn.CurJobDef != null && pawn.CurJobDef.defName == "MA_CarryAssistance")
            {
                return true;
            }

            foreach (Pawn _ in jobChecks.ActiveCarriersNeedingHelp(pawn.Map, pawn))
            {
                return false;
            }

            return true;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn == null || pawn.Map == null || t is not Pawn carrier)
            {
                return false;
            }

            if (carrier == pawn)
            {
                return false;
            }

            if (!jobChecks.IsValidCarrier(carrier))
            {
                return false;
            }

            if (jobChecks.IsAlreadyHelping(pawn, carrier))
            {
                return false;
            }

            int currentHelpers = jobChecks.CountHelpers(carrier, pawn.Map);
            if (currentHelpers >= 3)
            {
                Log.Message($"[CarryAssist] {pawn.LabelShortCap} BLOCKED: helper cap reached for {carrier.LabelShortCap} ({currentHelpers}/3)");
                return false;
            }

            return pawn.CanReach(carrier, PathEndMode.ClosestTouch, MaxPathDanger(pawn));
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!HasJobOnThing(pawn, t, forced))
            {
                return null;
            }

            if (t is not Pawn carrier || carrier.carryTracker?.CarriedThing == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(jobs.MA_DefOf.MA_CarryAssistance, carrier, carrier.carryTracker.CarriedThing);
            job.count = 1;
            return job;
        }
    }
}
