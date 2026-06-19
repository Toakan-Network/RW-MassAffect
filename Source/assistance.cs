using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RW_MassAffect
{
    public class JobChecks
    {
        private const int MaxAssistHelpers = 3;

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

                int currentHelpers = CountHelpers(p, map);
                if (currentHelpers >= MaxAssistHelpers)
                {
                    continue;
                }

                yield return p;
            }
        }

        public int CountHelpers(Pawn carrier, Map map)
        {
            if (carrier == null || map == null)
            {
                return 0;
            }

            return map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Count(p => p != carrier
                    && p.CurJobDef != null
                    && p.CurJobDef.defName == "MA_CarryAssistance"
                    && p.CurJob?.targetA.Thing == carrier);
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
