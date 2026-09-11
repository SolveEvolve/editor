using NUnit.Framework;
using UnityEngine;

namespace ProtoVR.Tests
{
    public sealed class FloorInteractionTests
    {
        [Test]
        public void GrabbableUsesRendererBoundsUnderMirroredParentAndLocksFloorPose()
        {
            var parent = new GameObject("Mirrored Parent");
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                parent.transform.localScale = new Vector3(1f, 1f, -1f);
                item.transform.SetParent(parent.transform, false);
                Object.DestroyImmediate(item.GetComponent<Collider>());

                var grabbable = item.AddComponent<FloorDistanceGrabbable>();
                grabbable.Configure(0.25f, 90f);

                Assert.That(
                    grabbable.TryRaycast(new Ray(new Vector3(0f, 0f, -5f), Vector3.forward), out float distance),
                    Is.True);
                Assert.That(distance, Is.GreaterThan(4f).And.LessThan(5f));

                grabbable.SetFloorPosition(new Vector3(2f, 8f, 3f));
                grabbable.RotateAroundWorldUp(20f);

                Assert.That(item.transform.position.y, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(Mathf.DeltaAngle(item.transform.eulerAngles.y, 20f), Is.Zero.Within(0.001f));
                Assert.That(Mathf.DeltaAngle(item.transform.eulerAngles.x, 0f), Is.Zero.Within(0.001f));
                Assert.That(Mathf.DeltaAngle(item.transform.eulerAngles.z, 0f), Is.Zero.Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void MicrophoneConeRingFollowsTargetExactly()
        {
            var cone = new GameObject("Cone");
            var target = new GameObject("Target");
            try
            {
                var line = cone.AddComponent<LineRenderer>();
                var follower = cone.AddComponent<MicrophoneConeFollower>();
                target.transform.position = new Vector3(3f, 0.2f, -4f);

                follower.Configure(target.transform, 10f, 16);

                Assert.That(line.positionCount, Is.EqualTo(48));
                Vector3 ringCenter = Vector3.zero;
                for (int index = 1; index < line.positionCount; index += 3)
                {
                    ringCenter += line.GetPosition(index);
                }

                ringCenter /= 16f;
                Assert.That(Vector3.Distance(cone.transform.TransformPoint(ringCenter), target.transform.position),
                    Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(cone);
                Object.DestroyImmediate(target);
            }
        }
    }
}
