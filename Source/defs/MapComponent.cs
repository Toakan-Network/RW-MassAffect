using RimWorld;
using Verse;
using Verse.AI;

namespace RW_MassAffect.defs
{
    public class CarryAssistMapComponent : Verse.MapComponent
    {
        public CarryAssistMapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 120 != 0 || map == null)
            {
                return;
            }

            foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (p.CurJobDef == null || p.CurJobDef.defName != "MA_CarryAssistance")
                {
                    continue;
                }

                Pawn carrier = p.CurJob?.targetA.Thing as Pawn;
                if (carrier == null || !carrier.Spawned || carrier.CurJobDef != RimWorld.JobDefOf.HaulToCell)
                {
                    p.jobs?.EndCurrentJob(JobCondition.Succeeded);
                }
            }
        }
    }
}
