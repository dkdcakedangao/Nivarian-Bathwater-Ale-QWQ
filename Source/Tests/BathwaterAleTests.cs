using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NivarianBathwaterAle;
using RimWorld;
using Verse;
using Verse.AI;

// 真实 1.7、本体驱动与 Harmony；仅隔离地图放置、图形、翻译和消息显示。
public static class BathwaterAleTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Harmony patch = new Harmony("local.bathwater-tests");
    private static Map map;
    private static Pawn pawn;
    private static Building tub;
    private static ThingDef race, tubDef;
    private static JobDef bathJob;
    private static Type rewardType;
    private static int checks, created, placed, notified, errors, warnings;
    private static bool blockPlacement, throwPlacement;
    private static TestWine lastWine;
    private static readonly Queue<int> rolls = new Queue<int>();
    private static string lastMessage;
    private static ThingPlaceMode lastMode;
    private static IntVec3 lastCell;
    private static object lastTarget;

    public sealed class TestWine : ThingWithComps
    {
        public bool DiscardedByTest;
        public TestWine(ThingDef definition)
        {
            def = definition;
            typeof(ThingWithComps).GetField("comps", F).SetValue(this,
                new List<ThingComp> { new CompQuality { parent = this } });
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish) { DiscardedByTest = true; }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    private static void Prefix(MethodBase method, string name)
    {
        patch.Patch(method, new HarmonyMethod(typeof(BathwaterAleTests).GetMethod(name, F)));
    }
    private static bool MapGetter(ref Map __result) { __result = map; return false; }
    private static bool PlayerFaction(ref Faction __result) { __result = null; return false; }
    private static bool LanguageWorkerGetter(ref LanguageWorker __result) { __result = new LanguageWorker_English(); return false; }
    private static bool FindSettingsType(string __0, ref Type __result)
    {
        if (__0 != typeof(BathwaterAleSettings).FullName) return true;
        __result = typeof(BathwaterAleSettings); return false;
    }
    private static bool Spawned(Thing __instance, ref bool __result)
    {
        __result = __instance == pawn || __instance == tub || (__instance == lastWine && placed > 0);
        return false;
    }
    private static bool Dead(ref bool __result) { __result = false; return false; }
    private static bool ShortLabel(ref string __result) { __result = "Nira"; return false; }
    private static bool MakeThing(ThingDef def, ref Thing __result)
    {
        created++;
        __result = lastWine = new TestWine(def);
        return false;
    }
    private static bool Place(object[] __args, ref bool __result)
    {
        lastCell = (IntVec3)__args[1]; lastMode = (ThingPlaceMode)__args[3];
        if (throwPlacement) throw new InvalidOperationException("Injected placement error");
        __result = !blockPlacement;
        if (__result) placed++;
        return false;
    }
    private static bool Message(object[] __args)
    {
        notified++; lastMessage = __args[0].ToString(); lastTarget = __args[1]; return false;
    }
    private static bool Translate(string __0, ref TaggedString __result)
    {
        __result = __0 == "BathwaterAle_Discovered" ? "{0} discovered {1}!" : __0;
        return false;
    }
    private static bool RandomRange(int minInclusive, int maxExclusive, ref int __result)
    {
        __result = rolls.Count == 0 ? minInclusive : rolls.Dequeue(); return false;
    }
    private static bool Error(string text) { errors++; Console.WriteLine("EXPECTED ERROR: " + text); return false; }
    private static bool Warning(string text) { if (text.StartsWith("[Nivarian Bathwater Ale]")) warnings++; return false; }
    private static bool EndJob(JobDriver __instance, JobCondition condition)
    {
        __instance.ended = true; __instance.Cleanup(condition); return false;
    }

    private static ThingDef Definition(string name)
    {
        var def = (ThingDef)RuntimeHelpers.GetUninitializedObject(typeof(ThingDef));
        def.defName = name; def.label = name; def.stackLimit = 75;
        DefDatabase<ThingDef>.Add(def);
        return def;
    }
    private static void Reset()
    {
        created = placed = notified = 0; blockPlacement = throwPlacement = false;
        lastWine = null; rolls.Clear(); BathwaterAleMod.Settings.TriggerChancePercent = 100;
        pawn.def = race; tub.def = tubDef;
    }
    private static JobDriver Driver()
    {
        var driver = (JobDriver)Activator.CreateInstance(bathJob.driverClass);
        driver.pawn = pawn; driver.job = new Job { def = bathJob, targetA = tub, ignoreForbidden = true };
        pawn.jobs = new Pawn_JobTracker(pawn);
        pawn.jobs.curJob = driver.job;
        pawn.stances = new Pawn_StanceTracker(pawn);
        return driver;
    }
    private static List<Toil> Toils(JobDriver driver)
    {
        return ((IEnumerable<Toil>)bathJob.driverClass.GetMethod("MakeNewToils", F).Invoke(driver, null)).ToList();
    }
    private static void Finish(JobDriver driver, List<Toil> toils)
    {
        foreach (Toil toil in toils) toil.actor = pawn;
        typeof(JobDriver).GetField("toils", F).SetValue(driver, toils);
        typeof(JobDriver).GetField("curToilIndex", F).SetValue(driver, 2);
        driver.ReadyForNextToil();
    }

    public static string Run(Assembly host, string savePath)
    {
        typeof(Prefs).GetField("data", F).SetValue(null, new PrefsData());
        typeof(UnityData).GetField("mainThreadId", F).SetValue(null, Environment.CurrentManagedThreadId);
        map = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
        pawn = new Pawn(); tub = new Building { Position = new IntVec3(4, 0, 7) };
        Prefix(typeof(Thing).GetProperty("Map").GetMethod, "MapGetter");
        Prefix(typeof(Faction).GetProperty("OfPlayer").GetMethod, "PlayerFaction");
        Prefix(typeof(Find).GetProperty("ActiveLanguageWorker").GetMethod, "LanguageWorkerGetter");
        Prefix(typeof(GenTypes).GetMethod("GetTypeInAnyAssembly"), "FindSettingsType");
        Prefix(typeof(Thing).GetProperty("Spawned").GetMethod, "Spawned");
        Prefix(typeof(Pawn).GetProperty("Dead").GetMethod, "Dead");
        Prefix(typeof(Pawn_StanceTracker).GetProperty("FullBodyBusy").GetMethod, "Dead");
        Prefix(typeof(Pawn).GetProperty("LabelShort").GetMethod, "ShortLabel");
        Prefix(typeof(ThingMaker).GetMethod("MakeThing"), "MakeThing");
        Prefix(typeof(GenPlace).GetMethods().Single(m => m.Name == "TryPlaceThing" && !m.GetParameters()[4].IsOut), "Place");
        Prefix(typeof(Messages).GetMethod("Message", new[] { typeof(string), typeof(LookTargets), typeof(MessageTypeDef), typeof(bool) }), "Message");
        Prefix(typeof(Translator).GetMethod("Translate", new[] { typeof(string) }), "Translate");
        Prefix(typeof(Rand).GetMethod("Range", new[] { typeof(int), typeof(int) }), "RandomRange");
        Prefix(typeof(Log).GetMethod("Error", new[] { typeof(string) }), "Error");
        Prefix(typeof(Log).GetMethod("Warning", new[] { typeof(string) }), "Warning");
        Prefix(typeof(JobDriver).GetMethod("EndJobWith"), "EndJob");
        try
        {
            race = Definition("NivarianRace_Pawn"); tubDef = Definition("Nivarian_Bathtub");
            Definition("Nivarian_FrostWheatAle"); Definition("Nivarian_FrostberryWine"); Definition("Nivarian_CrystalWine");
            bathJob = new JobDef { defName = "Nivarian_TakeBath", driverClass = host.GetType("Nivarian.AI.JobDriver_NivarianBath", true) };
            DefDatabase<JobDef>.Add(bathJob);
            BathwaterAleMod.Settings = new BathwaterAleSettings();
            Check(BathwaterAleMod.Settings.TriggerChancePercent == 10, "Default chance is not 10%");
            rewardType = typeof(BathwaterAleMod).Assembly.GetType("NivarianBathwaterAle.BathwaterAleReward");
            rewardType.GetMethod("Initialize", F).Invoke(null, null);
            Check(errors == 0, "Startup failed");
            var originalMethod = bathJob.driverClass.GetMethod("MakeNewToils", F);
            Check(Harmony.GetPatchInfo(originalMethod).Postfixes.Count(p => p.owner == "dkdcakedangao.NivarianBathwaterAleQWQ") == 1, "Expected one targeted postfix");
            rewardType.GetMethod("Initialize", F).Invoke(null, null);
            Check(Harmony.GetPatchInfo(originalMethod).Postfixes.Count(p => p.owner == "dkdcakedangao.NivarianBathwaterAleQWQ") == 1, "Repeated initialization duplicated patch");

            Reset();
            var driver = Driver(); var toils = Toils(driver);
            Check(toils.Count == 4 && toils[2].defaultDuration == 1250, "Original bath sequence changed");
            Check(toils[3].defaultCompleteMode == ToilCompleteMode.Instant && toils[3].tickAction == null && toils[3].tickIntervalAction == null, "Reward must be instantaneous with no ticks");
            Check(created == 0, "Enumerating job awarded wine early");
            Finish(driver, toils);
            Check(created == 1 && placed == 1 && notified == 1 && driver.ended, "Normal completion must award once and finish");
            Check(lastMode == ThingPlaceMode.Direct && lastCell == tub.Position, "Placement searched or used another cell");
            Check(lastWine.stackCount == 1 && !lastWine.DiscardedByTest, "Wrong quantity or successful wine destroyed");
            Check(lastMessage.Contains("Nira") && lastMessage.Contains("Nivarian_FrostWheatAle"), "Message missing pawn or wine name");
            Check(lastTarget is LookTargets, "Message lacks location target");

            Reset(); driver = Driver(); toils = Toils(driver);
            foreach (Toil toil in toils) toil.actor = pawn;
            typeof(JobDriver).GetField("toils", F).SetValue(driver, toils);
            typeof(JobDriver).GetField("curToilIndex", F).SetValue(driver, 2);
            driver.EndJobWith(JobCondition.InterruptForced);
            Check(created == 0 && notified == 0, "Interrupted bath awarded wine");
            Reset(); driver = Driver(); driver.ended = true; Toils(driver).Last().initAction();
            Check(created == 0, "Ended job awarded wine");
            Reset(); BathwaterAleMod.Settings.TriggerChancePercent = 0; Finish(DriverWithToils(out toils), toils);
            Check(created == 0 && notified == 0, "0% produced wine");
            Reset(); BathwaterAleMod.Settings.TriggerChancePercent = -50; Finish(DriverWithToils(out toils), toils);
            Check(created == 0, "Negative chance was not clamped");
            Reset(); BathwaterAleMod.Settings.TriggerChancePercent = 150; Finish(DriverWithToils(out toils), toils);
            Check(placed == 1, "Chance above 100 was not clamped");
            Reset(); pawn.def = Definition("OtherPawn"); Finish(DriverWithToils(out toils), toils);
            Check(created == 0, "Other race received reward");
            Reset(); tub.def = Definition("OtherTub"); Finish(DriverWithToils(out toils), toils);
            Check(created == 0, "Other tub received reward");

            string[] wines = { "Nivarian_FrostWheatAle", "Nivarian_FrostberryWine", "Nivarian_CrystalWine" };
            for (int i = 0; i < 3; i++)
            {
                Reset(); rolls.Enqueue(i); rolls.Enqueue(98); Finish(DriverWithToils(out toils), toils);
                Check(lastWine.def.defName == wines[i] && lastWine.GetComp<CompQuality>().Quality == QualityCategory.Masterwork,
                    "Wine choice or independently rolled quality failed");
            }
            int[] counts = new int[7];
            for (int roll = 0; roll < 100; roll++)
            {
                var quality = (QualityCategory)rewardType.GetMethod("QualityForRoll", F).Invoke(null, new object[] { roll });
                counts[(int)quality]++;
            }
            Check(counts.SequenceEqual(new[] { 5, 15, 40, 25, 10, 4, 1 }), "Quality weights or boundaries wrong");

            Reset(); blockPlacement = true; Finish(DriverWithToils(out toils), toils);
            Check(created == 1 && placed == 0 && notified == 0 && lastWine.DiscardedByTest, "Blocked placement leaked wine or notification");
            Check(warnings == 1, "Missing placement warning");
            Reset(); blockPlacement = true; Finish(DriverWithToils(out toils), toils);
            Check(warnings == 1, "Repeated placement warning was not deduplicated");
            Reset(); throwPlacement = true; Finish(DriverWithToils(out toils), toils);
            Check(placed == 0 && notified == 0 && lastWine.DiscardedByTest && errors == 1, "Placement exception not isolated");
            Reset(); throwPlacement = true; Finish(DriverWithToils(out toils), toils);
            Check(errors == 1, "Exception logs not deduplicated");

            BathwaterAleSettings settings = new BathwaterAleSettings { TriggerChancePercent = 37 };
            Scribe.saver.InitSaving(savePath, "BathwaterSettingsTest");
            Scribe_Deep.Look(ref settings, "settings"); Scribe.saver.FinalizeSaving();
            settings = null;
            Scribe.loader.InitLoading(savePath); Scribe_Deep.Look(ref settings, "settings"); Scribe.loader.FinalizeLoading();
            Check(settings.TriggerChancePercent == 37, "Settings Scribe roundtrip lost chance");
            settings.TriggerChancePercent = 200; settings.ExposeData();
            Check(settings.TriggerChancePercent == 100, "Settings upper clamp failed");
            settings.TriggerChancePercent = -1; settings.ExposeData();
            Check(settings.TriggerChancePercent == 0, "Settings lower clamp failed");
            Check(errors == 1, "Unexpected errors beyond the injected placement failure");
            return "PASS: " + checks + " checks; real host job patch, normal completion/interruption, probability edges, wine selection, weighted quality, direct placement, cleanup, log deduplication and settings Scribe. Rendering, real placement and full bath save/load require in-game testing.";
        }
        finally { patch.UnpatchAll(patch.Id); Scribe.ForceStop(); }
    }
    private static JobDriver DriverWithToils(out List<Toil> toils)
    {
        JobDriver driver = Driver(); toils = Toils(driver); return driver;
    }
}
