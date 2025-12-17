using Content.Server._RMC14.Popups;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Popups
{
    public sealed class PopupSystem : SharedPopupSystem
    {
        [Dependency] private readonly IPlayerManager _player = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly RMCPopupSystem _rmcPopup = default!;

        // Stories-Popup-Fix-Start
        /// <summary>
        /// Builds a popup visibility filter that respects a player's current view target.
        /// If the player has an <see cref="EyeComponent"/> with a valid <c>Target</c>,
        /// the visibility check is performed relative to that target's position instead of the player's own entity.
        /// Otherwise, the player's entity position is used.
        /// </summary>
        private Filter CreateEyeAwareFilter(EntityUid uid)
        {
            var filter = Filter.Empty();
            var popupPos = _transform.GetMapCoordinates(uid);
            var pvsRange = _cfg.GetCVar(Robust.Shared.CVars.NetMaxUpdateRange);

            foreach (var session in _player.Sessions)
            {
                if (session.AttachedEntity is not { } playerEntity)
                    continue;

                EntityUid viewEntity = playerEntity;

                if (TryComp<EyeComponent>(playerEntity, out var eyeComp) &&
                    eyeComp.Target is { } target &&
                    EntityManager.EntityExists(target))
                {
                    viewEntity = target;
                }

                var viewPos = _transform.GetMapCoordinates(viewEntity);

                if (viewPos.MapId == popupPos.MapId)
                {
                    var distance = (viewPos.Position - popupPos.Position).Length();
                    if (distance <= pvsRange)
                        filter.AddPlayer(session);
                }
                else
                {
                    filter.AddPlayer(session);
                }
            }

            return filter;
        }

        /// <summary>
        /// Same as <see cref="CreateEyeAwareFilter(EntityUid)"/>,
        /// but takes popup coordinates directly instead of an entity.
        /// Use when the popup is not tied to a specific entity.
        /// </summary>
        private Filter CreateEyeAwareFilterCoords(MapCoordinates popupPos)
        {
            var filter = Filter.Empty();
            var pvsRange = _cfg.GetCVar(Robust.Shared.CVars.NetMaxUpdateRange);

            foreach (var session in _player.Sessions)
            {
                if (session.AttachedEntity is not { } playerEntity)
                    continue;

                EntityUid viewEntity = playerEntity;

                if (TryComp<EyeComponent>(playerEntity, out var eyeComp) &&
                    eyeComp.Target is { } target &&
                    EntityManager.EntityExists(target))
                {
                    viewEntity = target;
                }

                var viewPos = _transform.GetMapCoordinates(viewEntity);

                if (viewPos.MapId == popupPos.MapId)
                {
                    var distance = (viewPos.Position - popupPos.Position).Length();
                    if (distance <= pvsRange)
                        filter.AddPlayer(session);
                }
                else
                {
                    filter.AddPlayer(session);
                }
            }

            return filter;
        }
        // Stories-Popup-Fix-End

        public override void PopupCursor(string? message, PopupType type = PopupType.Small)
        {
            // No local user.
        }

        public override void PopupCursor(string? message, ICommonSession recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            RaiseNetworkEvent(new PopupCursorEvent(message, type), recipient);
        }

        public override void PopupCursor(string? message, EntityUid recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            if (TryComp(recipient, out ActorComponent? actor))
                RaiseNetworkEvent(new PopupCursorEvent(message, type), actor.PlayerSession);
        }

        public override void PopupPredictedCursor(string? message, ICommonSession recipient, PopupType type = PopupType.Small)
        {
            // Do nothing, since the client already predicted the popup.
        }

        public override void PopupPredictedCursor(string? message, EntityUid recipient, PopupType type = PopupType.Small)
        {
            // Do nothing, since the client already predicted the popup.
        }

        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, Filter filter, bool replayRecord, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            RaiseNetworkEvent(new PopupCoordinatesEvent(message, type, GetNetCoordinates(coordinates)), filter, replayRecord);
        }

        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;
            var mapPos = _transform.ToMapCoordinates(coordinates);
            var filter = CreateEyeAwareFilterCoords(mapPos); // Stories-Popup-Fix
            RaiseNetworkEvent(new PopupCoordinatesEvent(message, type, GetNetCoordinates(coordinates)), filter);
        }

        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, ICommonSession recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            RaiseNetworkEvent(new PopupCoordinatesEvent(message, type, GetNetCoordinates(coordinates)), recipient);
        }

        public override void PopupCoordinates(string? message, EntityCoordinates coordinates, EntityUid recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            if (TryComp(recipient, out ActorComponent? actor))
                RaiseNetworkEvent(new PopupCoordinatesEvent(message, type, GetNetCoordinates(coordinates)), actor.PlayerSession);
        }

        public override void PopupPredictedCoordinates(string? message, EntityCoordinates coordinates, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            var mapPos = _transform.ToMapCoordinates(coordinates);
            var filter = CreateEyeAwareFilterCoords(mapPos); // Stories-Popup-Fix
            if (recipient != null)
            {
                // Don't send to recipient, since they predicted it locally
                filter = filter.RemovePlayerByAttachedEntity(recipient.Value);
            }
            RaiseNetworkEvent(new PopupCoordinatesEvent(message, type, GetNetCoordinates(coordinates)), filter);
        }

        public override void PopupEntity(string? message, EntityUid uid, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            var filter = CreateEyeAwareFilter(uid); // Stories-Popup-Fix
            RaiseNetworkEvent(new PopupEntityEvent(message, type, GetNetEntity(uid)), filter);
        }

        public override void PopupEntity(string? message, EntityUid uid, EntityUid recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            if (TryComp(recipient, out ActorComponent? actor))
                RaiseNetworkEvent(new PopupEntityEvent(message, type, GetNetEntity(uid)), actor.PlayerSession);
        }

        public override void PopupClient(string? message, EntityUid? recipient, PopupType type = PopupType.Small)
        {
        }

        public override void PopupClient(string? message, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            // do nothing duh its for client only
        }

        public override void PopupClient(string? message, EntityCoordinates coordinates, EntityUid? recipient, PopupType type = PopupType.Small)
        {
        }

        public override void PopupEntity(string? message, EntityUid uid, ICommonSession recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            RaiseNetworkEvent(new PopupEntityEvent(message, type, GetNetEntity(uid)), recipient);
        }

        public override void PopupEntity(string? message, EntityUid uid, Filter filter, bool recordReplay, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            RaiseNetworkEvent(new PopupEntityEvent(message, type, GetNetEntity(uid)), filter, recordReplay);
        }

        public override void PopupPredicted(string? message, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            if (recipient != null)
            {
                // RMC14 Check if popups should be shown to nearby players.
                if (!_rmcPopup.ShouldPopup(recipient.Value))
                    return;

                // Don't send to recipient, since they predicted it locally
                var filter = CreateEyeAwareFilter(uid).RemovePlayerByAttachedEntity(recipient.Value); // Stories-Popup-Fix
                RaiseNetworkEvent(new PopupEntityEvent(message, type, GetNetEntity(uid)), filter);
            }
            else
            {
                // With no recipient, send to everyone (in PVS range)
                var filter = CreateEyeAwareFilter(uid); // Stories-Popup-Fix
                RaiseNetworkEvent(new PopupEntityEvent(message, type, GetNetEntity(uid)), filter);
            }
        }

        public override void PopupPredicted(string? message, EntityUid uid, EntityUid? recipient, Filter filter, bool recordReplay, PopupType type = PopupType.Small)
        {
            if (message == null)
                return;

            if (recipient != null)
            {
                // Don't send to recipient, since they predicted it locally
                filter = filter.RemovePlayerByAttachedEntity(recipient.Value);
            }

            RaiseNetworkEvent(new PopupEntityEvent(message, type, GetNetEntity(uid)), filter, recordReplay);
        }

        public override void PopupPredicted(string? recipientMessage, string? othersMessage, EntityUid uid, EntityUid? recipient, PopupType type = PopupType.Small)
        {
            PopupPredicted(othersMessage, uid, recipient, type);
        }
    }
}
