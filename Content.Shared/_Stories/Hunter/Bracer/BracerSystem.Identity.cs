using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void InitializeIdentity()
    {
        SubscribeLocalEvent<HunterBracerComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
        SubscribeLocalEvent<HunterBracerComponent, MapInitEvent>(OnBracerIdentityInit);
    }

    private void OnBracerIdentityInit(Entity<HunterBracerComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<IdentityBlockerComponent>(ent);
        UpdateIdentity(ent.Owner, ent.Comp, null);
    }

    private void OnEquippedIdentity(Entity<HunterBracerComponent> ent, ref GotEquippedEvent args)
    {
        UpdateIdentity(ent.Owner, ent.Comp, args.Equipee);
    }

    private void OnUnequippedIdentity(Entity<HunterBracerComponent> ent, ref GotUnequippedEvent args)
    {
        UpdateIdentity(ent.Owner, ent.Comp, null);
    }

    private void OnTransformSpeakerName(Entity<HunterBracerComponent> ent, ref TransformSpeakerNameEvent args)
    {
        var wearer = Transform(ent).ParentUid;
        if (wearer.IsValid() && IsAuthorized(wearer, ent.Comp) && !ent.Comp.ShowClanName)
            args.VoiceName = Loc.GetString("identity-unknown-name");
    }

    private void UpdateIdentity(EntityUid bracerUid, HunterBracerComponent component, EntityUid? wearer)
    {
        if (TryComp<IdentityBlockerComponent>(bracerUid, out var blocker))
        {
            blocker.Enabled = false;

            if (wearer != null && IsAuthorized(wearer.Value, component) && !component.ShowClanName)
            {
                blocker.Enabled = true;
                blocker.Coverage = IdentityBlockerCoverage.FULL;
            }

            Dirty(bracerUid, blocker);
        }

        if (wearer != null)
        {
            var identitySys = EntityManager.System<SharedIdentitySystem>();
            identitySys.QueueIdentityUpdate(wearer.Value);
        }
    }
}
