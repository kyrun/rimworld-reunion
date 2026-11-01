using RimWorld;
using Verse;

namespace Kyrun.Reunion
{
    public static class IncidentAllyJoin
    {
        public static bool Do()
        {
            Map map = Current.Game.RandomPlayerHomeMap;
            IncidentWorker_AllyJoin worker = new IncidentWorker_AllyJoin();
            if (map == null || !worker.CanSpawnJoiner(map))
            {
                return false;
            }

            GameComponent gameComponent = GameComponent.CurrentGame;
            if (gameComponent == null) return false;

            Pawn pawn = gameComponent.GetRandomAllyForSpawning();
            if (pawn == null)
            {
                return false;
            }

            pawn.SetFactionDirect(Faction.OfPlayer);
            worker.SpawnJoiner(map, pawn);

            var def = IncidentDefs.Reunion_AllyJoin;
            TaggedString baseLetterLabel = def.letterLabel.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
            TaggedString baseLetterText = def.letterText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);

            Find.LetterStack.ReceiveLetter(baseLetterLabel, baseLetterText, def.letterDef, new LookTargets(pawn));

            gameComponent.TryScheduleNextEvent(ScheduleMode.Forced);

            return true;
        }

    }


    public class IncidentWorker_AllyJoin : IncidentWorker_WandererJoin
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            GameComponent gameComponent = GameComponent.CurrentGame;
            return gameComponent != null && gameComponent.ListAllyAvailable.Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return IncidentAllyJoin.Do();
        }
    }
}
