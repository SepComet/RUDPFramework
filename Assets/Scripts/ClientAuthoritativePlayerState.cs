using System;
using Network.Defines;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public sealed class ClientAuthoritativePlayerState
{
    public ClientAuthoritativePlayerStateSnapshot Current { get; private set; }

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
        return true;
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
        Position = SourceState.Position != null ? SourceState.Position.ToVector3() : Vector3.zero;
        Velocity = SourceState.Velocity != null ? SourceState.Velocity.ToVector3() : Vector3.zero;
        Rotation = SourceState.Rotation;
        Hp = SourceState.Hp;
    }

    public PlayerState SourceState { get; }

    public string PlayerId { get; }

    public long Tick { get; }

    public Vector3 Position { get; }

    public Vector3 Velocity { get; }

    public float Rotation { get; }

    public int Hp { get; }

    public Quaternion RotationQuaternion => Quaternion.Euler(0f, Rotation, 0f);
}
