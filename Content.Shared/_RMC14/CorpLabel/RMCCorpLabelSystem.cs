using Content.Shared.Examine;

namespace Content.Shared._RMC14.CorpLabel;

public sealed class RMCCorpLabelSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCCorpLabelComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<RMCCorpLabelComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(RMCCorpLabelComponent)))
        {
            args.PushMarkup(Loc.GetString("rmc-corp-label-examine",
                ("manufacturer", Loc.GetString(ent.Comp.Manufacturer))));
        }
    }
}
