using Content.Shared._RMC14.Xenonids.Empower;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.Xenonids.SuperEmpower;

public sealed class XenoSuperEmpowerVisualsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    [ValidatePrototypeId<ShaderPrototype>]
    private const string ShaderId = "SuperEmpower";
    private ShaderInstance? _shader;
    private EntityQuery<XenoEmpowerComponent> _activeSuperEmpowerQuery;


    public override void Initialize()
    {
        _activeSuperEmpowerQuery = GetEntityQuery<XenoEmpowerComponent>();
        _shader = _prototypeManager.Index<ShaderPrototype>(ShaderId).InstanceUnique();
        _shader.SetParameter("superpower_width", 2.7f);
    }

    public override void Update(float frameTime)
    {
        var SuperEmpower = EntityQueryEnumerator<XenoEmpowerComponent, SpriteComponent>();
        while (SuperEmpower.MoveNext(out var uid, out var comp, out var sprite))
        {
            if (comp.EmpowerActive)
            {
                var EmpowerTime = comp.EmpowerOffAt - _timing.CurTime;
                TimeSpan HalfTime = TimeSpan.FromSeconds(2);

                if (EmpowerTime > HalfTime)
                {
                    _shader?.SetParameter("superpower_width", 2.7f);
                    sprite.PostShader = _shader;
                }
                else
                {
                    _shader?.SetParameter("superpower_width", 1.7f);
                    sprite.PostShader = _shader;
                }

            }
            if (!comp.EmpowerActive && sprite.PostShader == _shader)
            {
                sprite.PostShader = null;
            }
        }
    }

}

