using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RW_MassAffect.defs
{
    public class WorkGiver_CarryAssistance : WorkGiver_Scanner
    {
        private readonly JobChecks jobChecks = new JobChecks();

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

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

            if (!pawn.CanReserve(carrier, 1, -1, null, forced))
            {
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
