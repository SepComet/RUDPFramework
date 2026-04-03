namespace Network.Defines
{
    public enum MessageType : byte
    {
        Unknow = 0,

        // Canonical dedicated-server MVP runtime vocabulary.
        MoveInput = 1,
        PlayerState = 2,
        ShootInput = 3,
        CombatEvent = 4,
        PlayerJoin = 5,

        LoginRequest = 21,
        LoginResponse = 22,
        LogoutRequest = 23,

        Heartbeat = 40,
        HeartbeatResponse = 41,
    }
}
