using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace SantaClausAnytime.Common
{
    public class SantaClausAnytimeSystem : ModSystem
    {
        void Log(string msg, string prefix = $"{nameof(SantaClausAnytimeSystem)}: ") => Mod.Logger.Info(prefix + msg);
        void LogError(string msg, string prefix = $"{nameof(SantaClausAnytimeSystem)}: ") => Mod.Logger.Error(prefix + msg);

        bool TryValidate<T>(T what, string error_msg)
        {
            if (what is null)
            {
                LogError(error_msg);
                return false;
            }
            return true;
        }

        // =======================================================================================

        MethodInfo GetMethod(string method_name)
        {
            return typeof(Main).GetMethod(
                method_name,
                BindingFlags.NonPublic | BindingFlags.Static
            );
        }

        MethodInfo GetNPCMethod(string method_name)
        {
            return typeof(NPC).GetMethod(
                method_name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        FieldInfo GetField(string field_name)
        {
            return typeof(Main).GetField(
                field_name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            );
        }

        bool TryGetXmasField(out FieldInfo xmas_field)
        {
            xmas_field = GetField(nameof(Main.xMas));
            return TryValidate(xmas_field, "Could not find Main.xMas field");
        }

        // =======================================================================================

        ILHook santaSpawnHook;
        ILHook santaKillHook;

        public override void Load()
        {
            var method = GetMethod("UpdateTime_SpawnTownNPCs");
            var npc_method = GetNPCMethod("AI_007_TownEntities");

            if (!TryValidate(method, "Could not find UpdateTime_SpawnTownNPCs")) return;
            if (!TryValidate(npc_method, "Could not find AI_007_TownEntities")) return;

            santaSpawnHook = new(method, MakeSantaSpawnCheckTrue);
            santaKillHook = new(npc_method, MakeSantaKillCheckFalse);
        }

        public override void Unload()
        {
            santaSpawnHook?.Dispose();
            santaSpawnHook = null;

            santaKillHook?.Dispose();
            santaKillHook = null;
        }

        // =======================================================================================

        void MakeSantaSpawnCheckTrue(ILContext il)
        {
            ILCursor cursor = new(il);

            if (!TryGetXmasField(out var xmas_field)) return;

            // loop through all xmas checks
            while (true)
            {
                bool xmas_check = cursor.TryGotoNext(
                    i => i.MatchLdsfld(xmas_field),
                    i => i.MatchBrfalse(out _)
                );
                if (!xmas_check) break;

                // replace Main.xMas with true
                // xmas will be true for santa only, and not other parts of the game
                cursor.Next.OpCode = OpCodes.Ldc_I4_1;
                cursor.Next.Operand = null;
            }

            Log("Made santa spawn check true");
        }

        // =======================================================================================

        void MakeSantaKillCheckFalse(ILContext il)
        {
            ILCursor cursor = new(il);

            if (!TryGetXmasField(out var xmas_field)) return;

            bool xmas_check = cursor.TryGotoNext(
                i => i.MatchLdsfld(xmas_field),
                i => i.MatchBrtrue(out _)
            );

            if (!xmas_check)
            {
                LogError("Could not find xmas check");
                MonoModHooks.DumpIL(Mod, il);
                return;
            }

            // make !Main.xMas become !true
            // which is false so it wont kill santa
            cursor.Next.OpCode = OpCodes.Ldc_I4_1;
            cursor.Next.Operand = null;
            Log("Made santa kill check false");
        }
    }
}
