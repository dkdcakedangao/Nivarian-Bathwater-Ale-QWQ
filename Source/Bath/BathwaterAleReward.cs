using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

// 执行的静态类，基本上就是绑定本体def。接入泡澡 Job
namespace NivarianBathwaterAle
{
    internal static class BathwaterAleReward
    {
        private const string LogPrefix = "[Nivarian Bathwater Ale] ";
        private static bool initialized;
        private static JobDef bathJob;
        private static ThingDef race;
        private static ThingDef bathtub;
        private static ThingDef[] wines;

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                // 固定
                bathJob = DefDatabase<JobDef>.GetNamedSilentFail("Nivarian_TakeBath");
                race = DefDatabase<ThingDef>.GetNamedSilentFail("NivarianRace_Pawn");
                bathtub = DefDatabase<ThingDef>.GetNamedSilentFail("Nivarian_Bathtub");
                wines = new[] {
                    DefDatabase<ThingDef>.GetNamedSilentFail("Nivarian_FrostWheatAle"),
                    DefDatabase<ThingDef>.GetNamedSilentFail("Nivarian_FrostberryWine"),
                    DefDatabase<ThingDef>.GetNamedSilentFail("Nivarian_CrystalWine")
                };
                if (bathJob == null || race == null || bathtub == null ||
                    wines[0] == null || wines[1] == null || wines[2] == null)
                    throw new InvalidOperationException("Missing Nivarian bath job, race, bathtub or wine definitions.");
                Type driver = bathJob.driverClass;
                if (driver == null || driver.FullName != "Nivarian.AI.JobDriver_NivarianBath" ||
                    !typeof(JobDriver).IsAssignableFrom(driver))
                    throw new InvalidOperationException("Unexpected Nivarian bath driver.");
                MethodInfo method = driver.GetMethod("MakeNewToils", BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (method == null || method.ReturnType != typeof(IEnumerable<Toil>))
                    throw new MissingMethodException(driver.FullName, "MakeNewToils");
                new Harmony("dkdcakedangao.NivarianBathwaterAleQWQ").Patch(method,
                    postfix: new HarmonyMethod(typeof(BathwaterAleReward), "AppendReward"));
            }
            catch (Exception error)
            {
                Log.ErrorOnce(LogPrefix + "泡澡产酒未启用；本体泡澡不受影响。Bath reward disabled: " + error, 1947102501);
            }
        }

        private static void AppendReward(JobDriver __instance, ref IEnumerable<Toil> __result)
        {
            __result = WithReward(__instance, __result);
        }

        private static IEnumerable<Toil> WithReward(JobDriver driver, IEnumerable<Toil> original)
        {
            foreach (Toil toil in original) yield return toil;
            Toil reward = ToilMaker.MakeToil("BathwaterAleReward");
            reward.defaultCompleteMode = ToilCompleteMode.Instant;
            reward.initAction = delegate { TryGiveReward(driver); };
            yield return reward;
        }

        // 核心！集千万石山代码的匠心之作！次回！糕蛋之亖！
        private static void TryGiveReward(JobDriver driver)
        {
            Thing wine = null;
            try
            {
                Pawn pawn = driver.pawn;
                Building tub = driver.job == null ? null : driver.job.targetA.Thing as Building;
                if (driver.ended || driver.job == null || driver.job.def != bathJob ||
                    pawn == null || pawn.Dead || pawn.Destroyed || pawn.def != race || !pawn.Spawned ||
                    tub == null || tub.def != bathtub || !tub.Spawned || pawn.Map != tub.Map)
                    return;

                int chance = Mathf.Clamp(BathwaterAleMod.Settings.TriggerChancePercent, 0, 100);
                if (chance == 0 || (chance < 100 && !Rand.Chance(chance / 100f))) return;

                ThingDef wineDef = wines[Rand.Range(0, wines.Length)];
                wine = ThingMaker.MakeThing(wineDef);
                wine.stackCount = 1;
                CompQuality quality = wine.TryGetComp<CompQuality>();
                if (quality == null) throw new InvalidOperationException(wineDef.defName + " has no CompQuality.");
                quality.SetQuality(QualityForRoll(Rand.Range(0, 100)), ArtGenerationContext.Colony);

                // 除错
                if (!GenPlace.TryPlaceThing(wine, tub.Position, tub.Map, ThingPlaceMode.Direct))
                {
                    Log.WarningOnce(LogPrefix + "浴缸格无法放酒，本次产出取消，不搜索或重试。", 1947102502);
                    return;
                }
                Messages.Message("BathwaterAle_Discovered".Translate(pawn.LabelShort, wineDef.label),
                    tub, MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception error)
            {
                Log.ErrorOnce(LogPrefix + "本次泡澡产酒结算异常，不重试。Bath reward failed: " + error, 1947102503);
            }
            finally
            {
                if (wine != null && !wine.Destroyed && !wine.Spawned)
                {
                    try { wine.Destroy(DestroyMode.Vanish); }
                    catch (Exception error) { Log.ErrorOnce(LogPrefix + "未放置酒的清理失败：" + error, 1947102504); }
                }
            }
        }

        private static QualityCategory QualityForRoll(int roll)
        {
            if (roll < 5) return QualityCategory.Awful;
            if (roll < 20) return QualityCategory.Poor;
            if (roll < 60) return QualityCategory.Normal;
            if (roll < 85) return QualityCategory.Good;
            if (roll < 95) return QualityCategory.Excellent;
            if (roll < 99) return QualityCategory.Masterwork;
            return QualityCategory.Legendary;
        }
    }
}
