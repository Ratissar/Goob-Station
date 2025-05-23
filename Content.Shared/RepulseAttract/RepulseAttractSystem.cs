// SPDX-FileCopyrightText: 2025 ActiveMammmoth <140334666+ActiveMammmoth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 ActiveMammmoth <kmcsmooth@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 keronshb <54602815+keronshb@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Physics;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using System.Numerics;
using Content.Shared.Weapons.Melee;

namespace Content.Shared.RepulseAttract;

public sealed class RepulseAttractSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedTransformSystem _xForm = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private HashSet<EntityUid> _entSet = new();
    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<RepulseAttractComponent, MeleeHitEvent>(OnMeleeAttempt, before: [typeof(UseDelayOnMeleeHitSystem)], after: [typeof(SharedWieldableSystem)]);
    }
    private void OnMeleeAttempt(Entity<RepulseAttractComponent> ent, ref MeleeHitEvent args)
    {
        if (_delay.IsDelayed(ent.Owner))
            return;

        TryRepulseAttract(ent, args.User);
    }

    public bool TryRepulseAttract(Entity<RepulseAttractComponent> ent, EntityUid user)
    {
        var position = _xForm.GetMapCoordinates(ent.Owner);
        return TryRepulseAttract(position, user, ent.Comp.Speed, ent.Comp.Range, ent.Comp.Whitelist, ent.Comp.CollisionMask);
    }

    public bool TryRepulseAttract(MapCoordinates position, EntityUid? user, float speed, float range, EntityWhitelist? whitelist = null, CollisionGroup layer = CollisionGroup.SingularityLayer)
    {
        _entSet.Clear();
        var epicenter = position.Position;
        _lookup.GetEntitiesInRange(position.MapId, epicenter, range, _entSet, flags: LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var target in _entSet)
        {
            if (!_physicsQuery.TryGetComponent(target, out var physics)
                || (physics.CollisionLayer & (int)layer) != 0x0) // exclude layers like ghosts
                continue;

            if (_whitelist.IsWhitelistFail(whitelist, target))
                continue;

            var targetPos = _xForm.GetWorldPosition(target);

            // vector from epicenter to target entity
            var direction = targetPos - epicenter;

            if (direction == Vector2.Zero)
                continue;

            // attract: throw all items directly to to the epicenter
            // repulse: throw them up to the maximum range
            var throwDirection = speed < 0 ? -direction : direction.Normalized() * (range - direction.Length());

            _throw.TryThrow(target, throwDirection, Math.Abs(speed), user, recoil: false, compensateFriction: true);
        }

        return true;
    }
}