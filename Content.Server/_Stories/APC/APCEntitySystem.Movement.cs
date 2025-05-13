using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Components;
using Content.Shared._RMC14.Xenonids.ScissorCut;
using Content.Shared._Stories.APC;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem
{
	private EntityQuery<PhysicsComponent> _physicsQuery;
	private EntityQuery<DestroyOnXenoPierceScissorComponent> _twoTestQuery;

	public void InitializeMovement()
	{
	    SubscribeLocalEvent<APCEntityComponent, PreventCollideEvent>(OnPreventCollide);
	    _physicsQuery = GetEntityQuery<PhysicsComponent>();
	    _twoTestQuery = GetEntityQuery<DestroyOnXenoPierceScissorComponent>();

	}

	private void OnPreventCollide(Entity<APCEntityComponent> apc, ref PreventCollideEvent args)
	{
		if (_timing.ApplyingState)
			return;

		if (!_physicsQuery.TryComp(apc, out var physics))
			return;

		if (physics.LinearVelocity.LengthSquared() < 0.1f)
			return;

	    if (_twoTestQuery.TryComp(args.OtherEntity, out var test))
	    {
	        args.Cancelled = true;
	        Del(args.OtherEntity);
	        _audio.PlayPvs(test.Sound, apc);
	    }
	}
}
