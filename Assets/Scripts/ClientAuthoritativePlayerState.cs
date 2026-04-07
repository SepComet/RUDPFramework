using System;
using Network.Defines;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public sealed class ClientAuthoritativePlayerState
{
    public ClientAuthoritativePlayerStateSnapshot Current { get; private set; }
    public ClientCombatPresentationSnapshot CombatPresentation { get; private set; } = ClientCombatPresentationSnapshot.Empty;

    public bool TryAccept(PlayerState state, out ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (Current != null && state.Tick <= Current.Tick)
        {
            snapshot = Current;
            return false;
        }

        snapshot = new ClientAuthoritativePlayerStateSnapshot(state);
        Current = snapshot;
        CombatPresentation = CombatPresentation.WithAuthoritativeSnapshot(snapshot);
        return true;
    }

    public bool TryApplyCombatEvent(CombatEvent combatEvent, string playerId, out ClientAuthoritativePlayerStateSnapshot snapshot, out ClientCombatPresentationSnapshot combatSnapshot)
    {
        if (combatEvent == null)
        {
            throw new ArgumentNullException(nameof(combatEvent));
        }

        if (string.IsNullOrEmpty(playerId))
        {
            throw new ArgumentException("Player id is required.", nameof(playerId));
        }

        if (!ClientCombatEventRouting.TryGetAffectedPlayerId(combatEvent, out var affectedPlayerId)
            || !string.Equals(affectedPlayerId, playerId, StringComparison.Ordinal))
        {
            snapshot = Current;
            combatSnapshot = CombatPresentation;
            return false;
        }

        Current = ApplyEventToCurrentSnapshot(combatEvent);
        CombatPresentation = CombatPresentation.WithCombatEvent(combatEvent, Current);

        snapshot = Current;
        combatSnapshot = CombatPresentation;
        return true;
    }

    private ClientAuthoritativePlayerStateSnapshot ApplyEventToCurrentSnapshot(CombatEvent combatEvent)
    {
        if (Current == null)
        {
            return null;
        }

        switch (combatEvent.EventType)
        {
            case CombatEventType.DamageApplied:
                return CloneSnapshotWithHp(Mathf.Max(0, Current.Hp - Mathf.Max(0, combatEvent.Damage)));
            case CombatEventType.Death:
                return CloneSnapshotWithHp(0);
            default:
                return Current;
        }
    }

    private ClientAuthoritativePlayerStateSnapshot CloneSnapshotWithHp(int hp)
    {
        if (Current == null)
        {
            return null;
        }

        var sourceState = Current.SourceState.Clone();
        sourceState.Hp = hp;
        return new ClientAuthoritativePlayerStateSnapshot(sourceState);
    }
}

public sealed class ClientAuthoritativePlayerStateSnapshot
{
    public ClientAuthoritativePlayerStateSnapshot(PlayerState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        SourceState = state.Clone();
        PlayerId = SourceState.PlayerId ?? string.Empty;
        Tick = SourceState.Tick;
        AcknowledgedMoveTick = SourceState.AcknowledgedMoveTick;
        Position = SourceState.Position != null ? SourceState.Position.ToVector3() : Vector3.zero;
        Velocity = SourceState.Velocity != null ? SourceState.Velocity.ToVector3() : Vector3.zero;
        Rotation = SourceState.Rotation;
        Hp = SourceState.Hp;
    }

    public PlayerState SourceState { get; }

    public string PlayerId { get; }

    public long Tick { get; }

    public long AcknowledgedMoveTick { get; }

    public Vector3 Position { get; }

    public Vector3 Velocity { get; }

    public float Rotation { get; }

    public int Hp { get; }

    public Quaternion RotationQuaternion => Quaternion.Euler(0f, NormalizeDegrees(Rotation), 0f);

    private static float NormalizeDegrees(float degrees)
    {
        var normalized = degrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }
}

public sealed class ClientCombatPresentationSnapshot
{
    public static readonly ClientCombatPresentationSnapshot Empty = new(false, CombatEventType.Unspecified, 0, 0, Vector3.zero, false);

    public ClientCombatPresentationSnapshot(
        bool hasLastEvent,
        CombatEventType lastEventType,
        long lastEventTick,
        int lastDamage,
        Vector3 lastHitPosition,
        bool isDead)
    {
        HasLastEvent = hasLastEvent;
        LastEventType = lastEventType;
        LastEventTick = lastEventTick;
        LastDamage = lastDamage;
        LastHitPosition = lastHitPosition;
        IsDead = isDead;
    }

    public bool HasLastEvent { get; }

    public CombatEventType LastEventType { get; }

    public long LastEventTick { get; }

    public int LastDamage { get; }

    public Vector3 LastHitPosition { get; }

    public bool IsDead { get; }

    public ClientCombatPresentationSnapshot WithAuthoritativeSnapshot(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return this;
        }

        return new ClientCombatPresentationSnapshot(
            HasLastEvent,
            LastEventType,
            LastEventTick,
            LastDamage,
            LastHitPosition,
            snapshot.Hp <= 0);
    }

    public ClientCombatPresentationSnapshot WithCombatEvent(CombatEvent combatEvent, ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        if (combatEvent == null)
        {
            throw new ArgumentNullException(nameof(combatEvent));
        }

        var isDead = combatEvent.EventType == CombatEventType.Death;
        if (!isDead && snapshot != null)
        {
            isDead = snapshot.Hp <= 0;
        }

        return new ClientCombatPresentationSnapshot(
            true,
            combatEvent.EventType,
            combatEvent.Tick,
            combatEvent.Damage,
            combatEvent.HitPosition != null ? combatEvent.HitPosition.ToVector3() : Vector3.zero,
            isDead);
    }
}
