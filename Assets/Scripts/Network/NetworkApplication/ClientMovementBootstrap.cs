using System;
using Network.Defines;

namespace Network.NetworkApplication
{
    public readonly struct ClientMovementBootstrap
    {
        public ClientMovementBootstrap(int authoritativeMoveSpeed, long serverTick)
        {
            AuthoritativeMoveSpeed = authoritativeMoveSpeed;
            ServerTick = serverTick;
        }

        public int AuthoritativeMoveSpeed { get; }

        public long ServerTick { get; }

        public static ClientMovementBootstrap FromLoginResponse(LoginResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            return new ClientMovementBootstrap(response.Speed, response.ServerTick);
        }
    }
}
