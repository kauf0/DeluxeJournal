using System.Reflection.Emit;
using Microsoft.Xna.Framework.Graphics;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using DeluxeJournal.Menus;

namespace DeluxeJournal.Patching
{
    /// <summary>Patches for <see cref="QuestLog"/>.</summary>
    internal class QuestLogPatch : PatchBase<QuestLogPatch>
    {
        public QuestLogPatch(IMonitor monitor) : base(monitor)
        {
            Instance = this;
        }

        private static bool Prefix_draw(QuestLog __instance, SpriteBatch b)
        {
            try
            {
                // !!! DO NOT DRAW THIS QUESTLOG AS THE ACTIVE MENU !!!
                // ----------------------------------------------------
                // 1) Prevents handling jittery frames being drawn while giving other mods a chance to replace the QuestLog.
                // 2) No logic should be done within draw(), so this SHOULD NOT impact a modded QuestLog.
                // 3) We only want to draw the QuestLog from within the QuestLogPage anyway.
                if (Game1.activeClickableMenu == __instance)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Instance.LogError(ex, nameof(Prefix_draw));
            }

            return true;
        }

        private static string GetTitle(string title)
        {
            try
            {
                if (Game1.activeClickableMenu is DeluxeJournalMenu journal && journal.ActivePage is QuestLogPage page)
                {
                    return page.Title;
                }
            }
            catch (Exception ex)
            {
                Instance.LogError(ex, nameof(GetTitle));
            }

            return title;
        }

        /// <summary>
        /// Inject a call to <see cref="GetTitle(string)"/> so that the <see cref="QuestLog"/> draws the title of the
        /// quests page instead of its own, rather than having it covered up by the <see cref="QuestLogPage"/>.
        /// </summary>
        private static IEnumerable<CodeInstruction> Transpiler_draw(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new(instructions);

            codeMatcher.MatchEndForward(
                    new CodeMatch(OpCodes.Ldstr, "Strings\\StringsFromCSFiles:QuestLog.cs.11373"),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(LocalizedContentManager), nameof(LocalizedContentManager.LoadString), new[] { typeof(string) }))
                )
                .ThrowIfNotMatch($"Could not find entry point for {nameof(Transpiler_draw)}")
                .Advance(1)
                .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(QuestLogPatch), nameof(GetTitle))));

            return codeMatcher.InstructionEnumeration();
        }

        public override void Apply(Harmony harmony)
        {
            Patch(harmony,
                original: AccessTools.Method(typeof(QuestLog), nameof(QuestLog.draw), new[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(QuestLogPatch), nameof(QuestLogPatch.Prefix_draw))
            );

            Patch(harmony,
                original: AccessTools.Method(typeof(QuestLog), nameof(QuestLog.draw), new[] { typeof(SpriteBatch) }),
                transpiler: new HarmonyMethod(typeof(QuestLogPatch), nameof(QuestLogPatch.Transpiler_draw))
            );
        }
    }
}
