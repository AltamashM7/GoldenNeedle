using GoldenNeedle.Gameplay.Hub;
using NUnit.Framework;
using UnityEngine;

namespace GoldenNeedle.Tests.Editor
{
    public sealed class HubPortalTriggerTests
    {
        [Test]
        public void PointInsideBox_UsesTheAuthoredRotatedVolume()
        {
            var gameObject = new GameObject("HubPortalTriggerVolumeTest");
            try
            {
                gameObject.transform.position = new Vector3(3f, 1f, -2f);
                gameObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.5f, 0f);
                box.size = new Vector3(2f, 2f, 1f);

                var inside = gameObject.transform.TransformPoint(box.center);
                var outside = inside + gameObject.transform.right * 1.1f;

                Assert.IsTrue(HubPortalTrigger.IsWorldPointInside(box, inside));
                Assert.IsFalse(HubPortalTrigger.IsWorldPointInside(box, outside));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PointInsideBox_ReturnsFalseForMissingVolume()
        {
            Assert.IsFalse(HubPortalTrigger.IsWorldPointInside(null, Vector3.zero));
        }
    }
}
