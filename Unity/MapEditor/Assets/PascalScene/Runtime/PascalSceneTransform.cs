using UnityEngine;

namespace PascalScene
{
    public static class PascalSceneTransform
    {
        public static Vector3 ToUnityPosition(float[] source)
        {
            return source == null || source.Length < 3
                ? Vector3.zero
                : new Vector3(source[0], source[1], source[2]);
        }

        public static Vector3 ToUnityScale(float[] source)
        {
            return source == null || source.Length < 3
                ? Vector3.one
                : new Vector3(source[0], source[1], source[2]);
        }

        public static Quaternion ToUnityRotationXyzRadians(float[] radians)
        {
            if (radians == null || radians.Length < 3)
            {
                return Quaternion.identity;
            }

            var hx = radians[0] * 0.5f;
            var hy = radians[1] * 0.5f;
            var hz = radians[2] * 0.5f;
            var sx = Mathf.Sin(hx);
            var cx = Mathf.Cos(hx);
            var sy = Mathf.Sin(hy);
            var cy = Mathf.Cos(hy);
            var sz = Mathf.Sin(hz);
            var cz = Mathf.Cos(hz);
            return new Quaternion(
                sx * cy * cz + cx * sy * sz,
                cx * sy * cz - sx * cy * sz,
                cx * cy * sz + sx * sy * cz,
                cx * cy * cz - sx * sy * sz);
        }
    }
}
