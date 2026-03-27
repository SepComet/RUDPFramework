namespace Network.NetworkApplication
{
    public enum SessionEventKind
    {
        TransportConnected = 0,
        LoginStarted = 1,
        LoginSucceeded = 2,
        LoginFailed = 3,
        HeartbeatSent = 4,
        HeartbeatReceived = 5,
        TimedOut = 6,
        ReconnectScheduled = 7,
        ReconnectStarted = 8,
        Disconnected = 9,
    }
}
