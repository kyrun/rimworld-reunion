using System.Reflection;
using Verse;
using HarmonyLib;

namespace Kyrun.Reunion
{
	[StaticConstructorOnStartup]
	class Main
	{
		static Main()
		{
			var harmony = new Harmony("kyrun.mod.reunion");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
	}
}
