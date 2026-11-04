using Il2CppEHG.Multiplayer;
using LastEpochItemGenerator;
using MelonLoader;

[assembly: MelonInfo(typeof(Mod), "Item Generator", "0.1.0", "asofianidis")]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]
[assembly: HarmonyDontPatchAll]

namespace LastEpochItemGenerator;

public sealed class Mod : MelonMod
{
    /// <summary>True only when the game itself reports offline play. Anything unexpected means off.</summary>
    internal static bool Offline
    {
        get
        {
            try { return !GameplayEnvironment.IsOnlinePlay; }
            catch { return false; }
        }
    }

    public override void OnInitializeMelon() => LoggerInstance.Msg("Ready. Press F4 in game to open the item generator.");

    public override void OnUpdate() => Menu.Update();

    public override void OnGUI() => Menu.Draw();
}
