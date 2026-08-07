using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    [Dependency] private readonly SharedIdentitySystem _identity = default!;

    private void InitializeIdentity()
    {
        SubscribeLocalEvent<HunterBracerComponent, InventoryRelayedEvent<TransformSpeakerNameEvent>>(OnTransformSpeakerName);
        
        SubscribeLocalEvent<HunterBracerComponent, MapInitEvent>(OnBracerIdentityInit);
    }

    private void OnBracerIdentityInit(Entity<HunterBracerComponent> ent, ref MapInitEvent args)
    {
        var blocker = EnsureComp<IdentityBlockerComponent>(ent);
        blocker.Enabled = !ent.Comp.ShowClanName;
        blocker.Coverage = IdentityBlockerCoverage.FULL;
    }

    private void OnEquippedIdentity(Entity<HunterBracerComponent> ent, ref GotEquippedEvent args)
    {
        UpdateBracerBlockerStatus(ent, args.Equipee);

        _identity.QueueIdentityUpdate(args.Equipee);
    }

    private void OnUnequippedIdentity(Entity<HunterBracerComponent> ent, ref GotUnequippedEvent args)
    {
        _identity.QueueIdentityUpdate(args.Equipee);
    }

    private void OnTransformSpeakerName(Entity<HunterBracerComponent> ent, ref InventoryRelayedEvent<TransformSpeakerNameEvent> args)
    {
        var user = args.Args.Sender;

        if (IsAuthorized(user, ent.Comp) && !ent.Comp.ShowClanName)
        {
            args.Args.VoiceName = Loc.GetString("identity-unknown-name");
        }
    }

    private void UpdateBracerBlockerStatus(Entity<HunterBracerComponent> ent, EntityUid? wearer)
    {
        if (!TryComp<IdentityBlockerComponent>(ent, out var blocker))
            return;

        var shouldBlock = false;

        if (wearer != null && IsAuthorized(wearer.Value, ent.Comp) && !ent.Comp.ShowClanName)
        {
            shouldBlock = true;
        }

        if (blocker.Enabled != shouldBlock)
        {
            blocker.Enabled = shouldBlock;
            Dirty(ent, blocker);

            if (wearer != null)
                _identity.QueueIdentityUpdate(wearer.Value);
        }
    }

    public void UpdateIdentity(EntityUid bracerUid, HunterBracerComponent component, EntityUid? wearer)
    {
        UpdateBracerBlockerStatus((bracerUid, component), wearer);
    }
}
