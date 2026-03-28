using System;
using System.Collections.Generic;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

public sealed class RemotePlayerSnapshotInterpolator
{
    // Keep remote rendering roughly two movement send intervals behind receive time so the client
    // usually has both an older and newer authoritative sample to interpolate between.
    public const float DefaultInterpolationDelaySeconds = 0.1f;
    public const int DefaultMaxBufferedSnapshots = 6;

    private readonly List<BufferedSnapshot> _snapshots = new();
    private readonly float _interpolationDelaySeconds;
    private readonly int _maxBufferedSnapshots;

    public RemotePlayerSnapshotInterpolator(
        float interpolationDelaySeconds = DefaultInterpolationDelaySeconds,
        int maxBufferedSnapshots = DefaultMaxBufferedSnapshots)
    {
        if (interpolationDelaySeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(interpolationDelaySeconds));
        }

        if (maxBufferedSnapshots < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBufferedSnapshots));
        }

        _interpolationDelaySeconds = interpolationDelaySeconds;
        _maxBufferedSnapshots = maxBufferedSnapshots;
    }

    public int BufferedSnapshotCount => _snapshots.Count;

    public long LatestBufferedTick => _snapshots.Count == 0 ? -1 : _snapshots[^1].Snapshot.Tick;

    public bool TryAddSnapshot(ClientAuthoritativePlayerStateSnapshot snapshot, float receivedAtSeconds)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (_snapshots.Count > 0 && snapshot.Tick <= _snapshots[^1].Snapshot.Tick)
        {
            return false;
        }

        _snapshots.Add(new BufferedSnapshot(snapshot, receivedAtSeconds));
        TrimOverflow();
        return true;
    }

    public RemotePlayerInterpolationSample Sample(float nowSeconds)
    {
        if (_snapshots.Count == 0)
        {
            return RemotePlayerInterpolationSample.None;
        }

        // Sample against a fixed delayed render timestamp. If the delay cannot be bracketed by two
        // buffered authoritative samples, clamp to the newest accepted snapshot instead of predicting.
        var renderTime = nowSeconds - _interpolationDelaySeconds;
        TrimConsumedSamples(renderTime);

        if (_snapshots.Count >= 2)
        {
            var from = _snapshots[0];
            var to = _snapshots[1];
            if (from.ReceivedAtSeconds <= renderTime && renderTime <= to.ReceivedAtSeconds)
            {
                var duration = to.ReceivedAtSeconds - from.ReceivedAtSeconds;
                var t = duration <= Mathf.Epsilon ? 1f : Mathf.Clamp01((renderTime - from.ReceivedAtSeconds) / duration);
                return RemotePlayerInterpolationSample.Interpolated(
                    Vector3.Lerp(from.Snapshot.Position, to.Snapshot.Position, t),
                    Quaternion.Slerp(from.Snapshot.RotationQuaternion, to.Snapshot.RotationQuaternion, t),
                    Vector3.Lerp(from.Snapshot.Velocity, to.Snapshot.Velocity, t),
                    to.Snapshot,
                    from.Snapshot,
                    t);
            }
        }

        return RemotePlayerInterpolationSample.Latest(_snapshots[^1].Snapshot);
    }

    private void TrimConsumedSamples(float renderTime)
    {
        while (_snapshots.Count >= 2 && _snapshots[1].ReceivedAtSeconds <= renderTime)
        {
            _snapshots.RemoveAt(0);
        }
    }

    private void TrimOverflow()
    {
        while (_snapshots.Count > _maxBufferedSnapshots)
        {
            _snapshots.RemoveAt(0);
        }
    }

    private readonly struct BufferedSnapshot
    {
        public BufferedSnapshot(ClientAuthoritativePlayerStateSnapshot snapshot, float receivedAtSeconds)
        {
            Snapshot = snapshot;
            ReceivedAtSeconds = receivedAtSeconds;
        }

        public ClientAuthoritativePlayerStateSnapshot Snapshot { get; }

        public float ReceivedAtSeconds { get; }
    }
}

public readonly struct RemotePlayerInterpolationSample
{
    private RemotePlayerInterpolationSample(
        bool hasValue,
        bool usedInterpolation,
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        ClientAuthoritativePlayerStateSnapshot latestSnapshot,
        ClientAuthoritativePlayerStateSnapshot fromSnapshot,
        float alpha)
    {
        HasValue = hasValue;
        UsedInterpolation = usedInterpolation;
        Position = position;
        Rotation = rotation;
        Velocity = velocity;
        LatestSnapshot = latestSnapshot;
        FromSnapshot = fromSnapshot;
        Alpha = alpha;
    }

    public static RemotePlayerInterpolationSample None { get; } =
        new(false, false, Vector3.zero, Quaternion.identity, Vector3.zero, null, null, 0f);

    public bool HasValue { get; }

    public bool UsedInterpolation { get; }

    public Vector3 Position { get; }

    public Quaternion Rotation { get; }

    public Vector3 Velocity { get; }

    public ClientAuthoritativePlayerStateSnapshot LatestSnapshot { get; }

    public ClientAuthoritativePlayerStateSnapshot FromSnapshot { get; }

    public float Alpha { get; }

    public static RemotePlayerInterpolationSample Latest(ClientAuthoritativePlayerStateSnapshot snapshot)
    {
        return new RemotePlayerInterpolationSample(
            true,
            false,
            snapshot.Position,
            snapshot.RotationQuaternion,
            snapshot.Velocity,
            snapshot,
            snapshot,
            1f);
    }

    public static RemotePlayerInterpolationSample Interpolated(
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        ClientAuthoritativePlayerStateSnapshot latestSnapshot,
        ClientAuthoritativePlayerStateSnapshot fromSnapshot,
        float alpha)
    {
        return new RemotePlayerInterpolationSample(
            true,
            true,
            position,
            rotation,
            velocity,
            latestSnapshot,
            fromSnapshot,
            alpha);
    }
}
