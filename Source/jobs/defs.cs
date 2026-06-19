using RimWorld;
using Verse;

namespace RW_MassAffect.jobs
{
    [DefOf]
    public static class MA_DefOf
    {
        static MA_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MA_DefOf));
        }

        public static JobDef MA_CarryAssistance;
        public static ThoughtDef MA_AssistedMe;
        public static ThoughtDef MA_AssistedTogether;
    }
}
