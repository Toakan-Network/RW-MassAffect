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
            Log.Message($"[CarryAssist] {pawn.LabelShortCap} TryMakePreToilReservations for {Carrier?.LabelShortCap ?? "null"} => bypassed");
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() =>
            {
                if (Carrier == null || Carrier.CurJobDef != JobDefOf.HaulToCell)
                {
                    Log.Message($"[CarryAssist] {pawn.LabelShortCap} STOP (FailOn): carrier not hauling");
                    return true;
                }
                return false;
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            var startToil = new Toil
            {
                initAction = () => Log.Message($"[CarryAssist] {pawn.LabelShortCap} START assisting {Carrier?.LabelShortCap ?? "null"}"),
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return startToil;

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
                    Log.Message($"[CarryAssist] {pawn.LabelShortCap} STOP assisting: carrier null/despawned");
                    return JobCondition.Succeeded;
                }

                if (Carrier.CurJobDef != JobDefOf.HaulToCell || Carrier.carryTracker?.CarriedThing is not Corpse)
                {
                    Log.Message($"[CarryAssist] {pawn.LabelShortCap} STOP assisting {Carrier.LabelShortCap}: carrier no longer hauling a corpse");
                    return JobCondition.Succeeded;
                }

                return JobCondition.Ongoing;
            });

            yield return assistToil;
        }
    }
}
