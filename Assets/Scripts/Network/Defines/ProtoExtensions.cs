using UnityEngine;

namespace Network.Defines
{
    public static class ProtoExtensions
    {
        public static UnityEngine.Vector3 ToVector3(this Vector3 vec)
        {
            return new UnityEngine.Vector3()
            {
                x = vec.X,
                y = vec.Y,
                z = vec.Z
            };
        }

        public static Vector3 ToProtoVector3(UnityEngine.Vector3 vec)
        {
            return new Vector3()
            {
                X = vec.x,
                Y = vec.y,
                Z = vec.z
            };
        }
    }
}