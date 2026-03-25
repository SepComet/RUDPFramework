namespace Network.Defines
{
    public enum MessageType : byte
    {
        Unknow = 0,

        // 游戏相关
        PlayerInput = 1,
        PlayerState = 2,
        PlayerAction = 3,
        GameState = 4,
        PlayerJoin = 5,
        PlayerLeave = 6,

        // 聊天相关
        ChatMessage = 10,
        PrivateMessage = 11,
        SystemMessage = 12,

        // 系统相关
        HeartBeat = 20,

        LoginRequest = 21,
        LoginResponse = 22,

        LogoutRequest = 23,

        // 房间管理
        CreateRoom = 30,
        JoinRoom = 31,
        LeaveRoom = 32,
        RoomList = 33,
        
        Heartbeat = 40,
        HeartbeatResponse = 41,
    }
}

