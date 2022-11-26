using System.Reflection;
using Verse;
using HarmonyLib;

namespace Kyrun.Reunion
{
    [StaticConstructorOnStartup]
    class Main
    {
        public static Harmony HarmonyInstance;

        static Main()
        {
            HarmonyInstance = new Harmony("kyrun.mod.reunion");
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
