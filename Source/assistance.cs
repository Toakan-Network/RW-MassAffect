using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RW_MassAffect
{
    public class JobChecks
    {
        public IEnumerable<Pawn> ActiveCarriersNeedingHelp(Map map, Pawn helper)
        {
            if (map == null)
            {
                yield break;
            }

            foreach (Pawn p in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (!IsValidCarrier(p))
                {
                    continue;
                }

                if (helper != null && IsAlreadyHelping(helper, p))
                {
                    continue;
                }

                yield return p;
            }
        }

        public bool IsValidCarrier(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && pawn.CurJobDef == JobDefOf.HaulToCell
                && pawn.carryTracker?.CarriedThing is Corpse;
        }

        public bool IsAlreadyHelping(Pawn helper, Pawn carrier)
        {
            return helper != null
                && carrier != null
                && helper.CurJobDef != null
                && helper.CurJobDef.defName == "MA_CarryAssistance"
                && helper.CurJob?.targetA.Thing == carrier;
        }
    }
}
