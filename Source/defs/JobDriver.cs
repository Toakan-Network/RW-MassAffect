using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RW_MassAffect.defs
{
    public class JobDriver_CarryAssistance : Verse.AI.JobDriver
    {
        private Pawn Carrier => job.GetTarget(TargetIndex.A).Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Carrier == null || Carrier.CurJobDef != JobDefOf.HaulToCell || Carrier.carryTracker?.CarriedThing is not Corpse);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            var assistToil = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Never,
                tickAction = () =>
                {
                    if (Carrier == null || !Carrier.Spawned || Carrier.CurJobDef != JobDefOf.HaulToCell)
                    {
                        ReadyForNextToil();
                        return;
                    }

                    if (pawn.pather == null || pawn.Downed)
                    {
                        return;
                    }

                    if (!pawn.Position.InHorDistOf(Carrier.Position, 1.9f) && pawn.pather.Moving)
                    {
                        return;
                    }

                    if (!pawn.Position.InHorDistOf(Carrier.Position, 1.9f))
                    {
                        pawn.pather.StartPath(Carrier, PathEndMode.Touch);
                    }
                }
            };

            assistToil.AddEndCondition(() =>
            {
                if (Carrier == null || !Carrier.Spawned)
                {
                    return JobCondition.Succeeded;
                }

                if (Carrier.CurJobDef != JobDefOf.HaulToCell || Carrier.carryTracker?.CarriedThing is not Corpse)
                {
                    return JobCondition.Succeeded;
                }

                return JobCondition.Ongoing;
            });

            yield return assistToil;
        }
    }
}
