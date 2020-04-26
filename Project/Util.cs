using System.Collections.Generic;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

namespace Kyrun.Reunion
{
	static class Util
	{
		public const string DELIMITER = ", ";

		public static void Msg(object o)
		{
			Log.Message("[Reunion] " + o);
		}

		public static void Warn(object o)
		{
			Log.Warning("[Reunion] " + o);
		}

		public static void PrintAllyList()
		{
			var str = "";
			foreach (var ally in GameComponent.ListAllyAvailable)
			{
				str += ally.Name + DELIMITER;
			}
			if (str != "") str = str.Substring(0, str.Length - DELIMITER.Length); // truncate last delimiter
			Msg("Ally Pawns: " + str);
		}

		// From virtual function (since calling base.Notify_GeneratedByQuestGen will run the parent and not the grandparent)
		public static void SitePartWorker_Base_Notify_GeneratedByQuestGen(SitePart part, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
		{
			outExtraDescriptionRules.AddRange(GrammarUtility.RulesForDef("", part.def));
			outExtraDescriptionConstants.Add("sitePart", part.def.defName);
		}
	}
}
