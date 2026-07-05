using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.main_menu_state", "aetheria.main_menu_state.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaMainMenuState
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "Aetheria main menu state";

    [Key(1)]
    public string ActiveSurfaceId { get; set; } = "";

    [Key(2)]
    public string LastCommandId { get; set; } = "";

    [Key(3)]
    public string LastCommand { get; set; } = "";

    [Key(4)]
    public string UpdatedAtUtc { get; set; } = "";
}
