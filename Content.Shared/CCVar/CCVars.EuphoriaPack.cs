using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

/// <summary>
/// Station7-specific CVars.
/// </summary>
[CVarDefs]
public sealed partial class CCVarsStation7
{
    /// <summary>
    /// How long (in seconds) the server must be empty during a round before the round
    /// is automatically ended and returned to lobby. Set to 0 to disable.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    public static readonly CVarDef<float> EmptyServerRoundEndDelay =
        CVarDef.Create("game.empty_server_round_end_delay", 300f, CVar.SERVERONLY);
}
