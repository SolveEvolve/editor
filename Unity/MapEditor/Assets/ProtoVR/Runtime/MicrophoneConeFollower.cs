using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoVR
{
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class MicrophoneConeFollower : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField, Min(0.1f)] private float _halfAngleDegrees = 10f;
        [SerializeField, Range(3, 32)] private int _segments = 16;

        private LineRenderer _line;

        public Transform Target => _target;

        public void Configure(Transform target, float halfAngleDegrees, int segments = 16)
        {
            _target = target;
            _halfAngleDegrees = Mathf.Max(0.1f, halfAngleDegrees);
            _segments = Mathf.Clamp(segments, 3, 32);
            UpdateCone();
        }

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.shadowCastingMode = ShadowCastingMode.Off;
            UpdateCone();
        }

        private void LateUpdate()
        {
            UpdateCone();
        }

        private void OnValidate()
        {
            _segments = Mathf.Clamp(_segments, 3, 32);
            _halfAngleDegrees = Mathf.Max(0.1f, _halfAngleDegrees);
            UpdateCone();
        }

        private void UpdateCone()
        {
            if (_target == null)
            {
                return;
            }

            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
            }

            if (_line == null)
            {
                return;
            }

            _line.useWorldSpace = false;
            _line.loop = false;
            _line.shadowCastingMode = ShadowCastingMode.Off;

            Vector3 target = transform.InverseTransformPoint(_target.position);
            float length = target.magnitude;
            if (length < 0.0001f)
            {
                _line.positionCount = 0;
                return;
            }

            Vector3 axis = target / length;
            Vector3 tangent = Vector3.Cross(axis, Vector3.up);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.Cross(axis, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;
            float radius = Mathf.Tan(_halfAngleDegrees * Mathf.Deg2Rad) * length;

            int positionCount = _segments * 3;
            _line.positionCount = positionCount;
            for (int segment = 0; segment < _segments; segment++)
            {
                float angleA = segment * Mathf.PI * 2f / _segments;
                float angleB = (segment + 1) * Mathf.PI * 2f / _segments;
                Vector3 rimA = target + radius * (Mathf.Cos(angleA) * tangent + Mathf.Sin(angleA) * bitangent);
                Vector3 rimB = target + radius * (Mathf.Cos(angleB) * tangent + Mathf.Sin(angleB) * bitangent);

                int index = segment * 3;
                _line.SetPosition(index, Vector3.zero);
                _line.SetPosition(index + 1, rimA);
                _line.SetPosition(index + 2, rimB);
            }
        }
    }
}
