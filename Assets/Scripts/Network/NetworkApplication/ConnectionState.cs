using System;

namespace Network.NetworkApplication
{
    public enum ConnectionState
    {
        Disconnected = 0,
        TransportConnected = 1,
        LoginPending = 2,
        LoggedIn = 3,
        LoginFailed = 4,
        TimedOut = 5,
        ReconnectPending = 6,
        Reconnecting = 7,
    }
}
