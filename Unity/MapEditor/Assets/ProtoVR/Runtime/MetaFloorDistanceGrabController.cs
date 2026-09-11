using UnityEngine;
using UnityEngine.Rendering;

namespace ProtoVR
{
    [DisallowMultipleComponent]
    public sealed class MetaFloorDistanceGrabController : MonoBehaviour
    {
        [SerializeField] private Transform _leftController;
        [SerializeField] private Transform _rightController;
        [SerializeField] private LineRenderer _leftLaser;
        [SerializeField] private LineRenderer _rightLaser;
        [SerializeField] private float _maximumDistance = 30f;
        [SerializeField, Range(0f, 1f)] private float _rotationDeadZone = 0.2f;

        private readonly ControllerState _leftState = new ControllerState();
        private readonly ControllerState _rightState = new ControllerState();
        private FloorDistanceGrabbable[] _grabbables;
        private MaterialPropertyBlock _laserProperties;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color IdleLaserColor = new Color(0.1f, 0.65f, 1f, 1f);
        private static readonly Color ActiveLaserColor = new Color(0.2f, 1f, 0.45f, 1f);

        private sealed class ControllerState
        {
            public FloorDistanceGrabbable Grabbed;
            public Vector3 PlaneOffset;
            public float GrabDistance;
            public bool TriggerPressed;
        }

        public void Configure(
            Transform leftController,
            Transform rightController,
            LineRenderer leftLaser,
            LineRenderer rightLaser)
        {
            _leftController = leftController;
            _rightController = rightController;
            _leftLaser = leftLaser;
            _rightLaser = rightLaser;
        }

        private void Awake()
        {
            _grabbables = FindObjectsByType<FloorDistanceGrabbable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _laserProperties = new MaterialPropertyBlock();
            ConfigureLaser(_leftLaser);
            ConfigureLaser(_rightLaser);
        }

        private void Update()
        {
            UpdateController(_leftController, _leftLaser, OVRInput.Controller.LTouch, _leftState);
            UpdateController(_rightController, _rightLaser, OVRInput.Controller.RTouch, _rightState);
        }

        private void UpdateController(
            Transform controller,
            LineRenderer laser,
            OVRInput.Controller controllerMask,
            ControllerState state)
        {
            if (controller == null || laser == null)
            {
                return;
            }

            Ray ray = new Ray(controller.position, controller.forward);
            FloorDistanceGrabbable hovered = FindNearestGrabbable(ray, out float laserDistance);

            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controllerMask);
            bool triggerPressed = state.TriggerPressed ? trigger > 0.25f : trigger > 0.65f;

            if (!state.TriggerPressed && triggerPressed && hovered != null)
            {
                BeginGrab(ray, hovered, state);
            }
            else if (state.TriggerPressed && !triggerPressed)
            {
                state.Grabbed = null;
            }

            state.TriggerPressed = triggerPressed;

            if (state.Grabbed != null)
            {
                UpdateGrab(ray, controllerMask, state);
                laserDistance = Mathf.Min(_maximumDistance, Vector3.Distance(ray.origin, state.Grabbed.transform.position));
            }

            laser.SetPosition(0, ray.origin);
            laser.SetPosition(1, ray.GetPoint(laserDistance));
            SetLaserColor(laser, state.Grabbed != null ? ActiveLaserColor : IdleLaserColor);
        }

        private FloorDistanceGrabbable FindNearestGrabbable(Ray ray, out float laserDistance)
        {
            FloorDistanceGrabbable nearestGrabbable = null;
            float nearestGrabbableDistance = float.PositiveInfinity;
            laserDistance = _maximumDistance;

            foreach (FloorDistanceGrabbable grabbable in _grabbables)
            {
                if (grabbable != null &&
                    grabbable.TryRaycast(ray, out float hitDistance) &&
                    hitDistance <= _maximumDistance &&
                    hitDistance < nearestGrabbableDistance)
                {
                    nearestGrabbable = grabbable;
                    nearestGrabbableDistance = hitDistance;
                }
            }

            if (nearestGrabbable != null)
            {
                laserDistance = nearestGrabbableDistance;
            }

            return nearestGrabbable;
        }

        private static void BeginGrab(Ray ray, FloorDistanceGrabbable grabbable, ControllerState state)
        {
            state.Grabbed = grabbable;
            state.GrabDistance = Vector3.Distance(ray.origin, grabbable.transform.position);

            Vector3 planePoint = ProjectRayToFloor(ray, grabbable.FloorHeight, state.GrabDistance);
            state.PlaneOffset = grabbable.transform.position - planePoint;
            state.PlaneOffset.y = 0f;
        }

        private void UpdateGrab(Ray ray, OVRInput.Controller controllerMask, ControllerState state)
        {
            FloorDistanceGrabbable grabbable = state.Grabbed;
            Vector3 planePoint = ProjectRayToFloor(ray, grabbable.FloorHeight, state.GrabDistance);
            grabbable.SetFloorPosition(planePoint + state.PlaneOffset);

            float rotation = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controllerMask).x;
            if (Mathf.Abs(rotation) >= _rotationDeadZone)
            {
                grabbable.RotateAroundWorldUp(rotation * grabbable.RotationSpeed * Time.deltaTime);
            }
        }

        private static Vector3 ProjectRayToFloor(Ray ray, float floorHeight, float fallbackDistance)
        {
            Plane floor = new Plane(Vector3.up, new Vector3(0f, floorHeight, 0f));
            if (floor.Raycast(ray, out float distance) && distance >= 0f)
            {
                return ray.GetPoint(distance);
            }

            Vector3 fallback = ray.GetPoint(fallbackDistance);
            fallback.y = floorHeight;
            return fallback;
        }

        private static void ConfigureLaser(LineRenderer laser)
        {
            if (laser == null)
            {
                return;
            }

            laser.positionCount = 2;
            laser.useWorldSpace = true;
            laser.loop = false;
            laser.startWidth = 0.006f;
            laser.endWidth = 0.003f;
            laser.shadowCastingMode = ShadowCastingMode.Off;
            laser.receiveShadows = false;
            laser.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void SetLaserColor(LineRenderer laser, Color color)
        {
            laser.GetPropertyBlock(_laserProperties);
            _laserProperties.SetColor(BaseColor, color);
            laser.SetPropertyBlock(_laserProperties);
        }
    }
}
