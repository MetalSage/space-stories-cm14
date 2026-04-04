
using Content.Server.Maps;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.GameTicking.Presets
{
    /// <summary>
    ///     A round-start setup preset, such as which antagonists to spawn.
    /// </summary>
    [Prototype]
    public sealed partial class GamePresetPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("alias")]
        public string[] Alias = Array.Empty<string>();

        [DataField("name")]
        public string ModeTitle = "????";

        [DataField("description")]
        public string Description = string.Empty;

        [DataField("showInVote")]
        public bool ShowInVote;

        [DataField("minPlayers")]
        public int? MinPlayers;

        [DataField("maxPlayers")]
        public int? MaxPlayers;

        // Stories-LowPop-Start
        /// <summary>
        /// If set, this preset acts as a round-start preset family.
        /// The server resolves it into one of the listed concrete presets right before the round starts.
        /// The first preset whose player requirements match is selected.
        /// </summary>
        [DataField("roundStartResolvePresets", customTypeSerializer: typeof(PrototypeIdListSerializer<GamePresetPrototype>))]
        public IReadOnlyList<string> RoundStartResolvePresets { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Excludes this preset from round start auto-selection during the first lobby after a server restart.
        /// This is used by special variants, such as low population presets, that should not be auto-picked
        /// before the lobby has had time to refill after the server comes back online.
        /// </summary>
        [DataField("ignoreOnFirstRoundAfterRestart")]
        public bool IgnoreOnFirstRoundAfterRestart;
        // Stories-LowPop-End

        [DataField("rules", customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
        public IReadOnlyList<string> Rules { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// If specified, the gamemode will only be run with these maps.
        /// If none are elligible, the global fallback will be used.
        /// </summary>
        [DataField("supportedMaps", customTypeSerializer: typeof(PrototypeIdSerializer<GameMapPoolPrototype>))]
        public string? MapPool;
    }
}
