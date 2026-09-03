using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Rotation;
using UnityEngine;

namespace GoldenNeedle.Debug.PoseTrackingSpike
{
    /// <summary>
    /// Runtime-created acceptance rig for the Motion Engine Lab. It is deliberately simple and
    /// keeps authored joint offsets/local scales independent from retargeted rotations.
    /// </summary>
    public sealed class ProceduralDebugHumanoidRig : MonoBehaviour, IExplicitHumanoidRigSource, IExplicitHumanoidChainSource
    {
        private Transform _avatarRoot;
        private Transform _hips;
        private Transform _spine;
        private Transform _chest;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _leftHand;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _rightHand;
        private Transform _leftUpperLeg;
        private Transform _leftLowerLeg;
        private Transform _leftFoot;
        private Transform _rightUpperLeg;
        private Transform _rightLowerLeg;
        private Transform _rightFoot;
        private Material _segmentMaterial;
        private Material _targetMarkerMaterial;
        private Material _hintMarkerMaterial;
        private Transform _markerRoot;
        private readonly Transform[] _targetMarkers = new Transform[CanonicalKinematicTargets.ChainCount];
        private readonly Transform[] _hintMarkers = new Transform[CanonicalKinematicTargets.ChainCount];

        public bool IsBuilt => _avatarRoot != null;
        public Transform AvatarRoot
        {
            get
            {
                BuildOnce();
                return _avatarRoot;
            }
        }

        private void Awake()
        {
            BuildOnce();
        }

        public bool TryGetBinding(Transform[] destinationBones, out Transform avatarRoot)
        {
            BuildOnce();
            avatarRoot = _avatarRoot;
            if (destinationBones == null || destinationBones.Length < CanonicalRotationFrame.BoneCount || avatarRoot == null)
            {
                return false;
            }

            destinationBones[(int)CanonicalBoneId.Pelvis] = _hips;
            destinationBones[(int)CanonicalBoneId.Chest] = _chest;
            destinationBones[(int)CanonicalBoneId.LeftUpperArm] = _leftUpperArm;
            destinationBones[(int)CanonicalBoneId.LeftLowerArm] = _leftLowerArm;
            destinationBones[(int)CanonicalBoneId.RightUpperArm] = _rightUpperArm;
            destinationBones[(int)CanonicalBoneId.RightLowerArm] = _rightLowerArm;
            destinationBones[(int)CanonicalBoneId.LeftUpperLeg] = _leftUpperLeg;
            destinationBones[(int)CanonicalBoneId.LeftLowerLeg] = _leftLowerLeg;
            destinationBones[(int)CanonicalBoneId.RightUpperLeg] = _rightUpperLeg;
            destinationBones[(int)CanonicalBoneId.RightLowerLeg] = _rightLowerLeg;
            return true;
        }

        public bool TryGetChainTip(CanonicalKinematicChainId chainId, out Transform tip)
        {
            BuildOnce();
            switch (chainId)
            {
                case CanonicalKinematicChainId.LeftArm:
                    tip = _leftHand;
                    return tip != null;
                case CanonicalKinematicChainId.RightArm:
                    tip = _rightHand;
                    return tip != null;
                case CanonicalKinematicChainId.LeftLeg:
                    tip = _leftFoot;
                    return tip != null;
                case CanonicalKinematicChainId.RightLeg:
                    tip = _rightFoot;
                    return tip != null;
                default:
                    tip = null;
                    return false;
            }
        }

        public void SetKinematicMarkers(
            CanonicalKinematicChainId chainId,
            bool showTarget,
            Vector3 targetPosition,
            bool showHint,
            Vector3 hintPosition)
        {
            BuildOnce();
            var index = (int)chainId;
            if (index < 0 || index >= _targetMarkers.Length)
            {
                return;
            }

            if (_targetMarkers[index] != null)
            {
                _targetMarkers[index].gameObject.SetActive(showTarget && IsFinite(targetPosition));
                if (showTarget && IsFinite(targetPosition))
                {
                    _targetMarkers[index].position = targetPosition;
                }
            }

            if (_hintMarkers[index] != null)
            {
                _hintMarkers[index].gameObject.SetActive(showHint && IsFinite(hintPosition));
                if (showHint && IsFinite(hintPosition))
                {
                    _hintMarkers[index].position = hintPosition;
                }
            }
        }

        public void ClearKinematicMarkers()
        {
            for (var i = 0; i < _targetMarkers.Length; i++)
            {
                if (_targetMarkers[i] != null)
                {
                    _targetMarkers[i].gameObject.SetActive(false);
                }

                if (_hintMarkers[i] != null)
                {
                    _hintMarkers[i].gameObject.SetActive(false);
                }
            }
        }

        private void BuildOnce()
        {
            if (_avatarRoot != null)
            {
                return;
            }

            _avatarRoot = CreateBone("DebugAvatarRoot", transform, new Vector3(0f, 0f, 3f));
            // The debug avatar faces the webcam. Its local +Z is its anatomical forward, which
            // therefore points toward the camera (-Z in the canonical webcam frame).
            _avatarRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _segmentMaterial = CreateSegmentMaterial();
            _hips = CreateBone("Hips", _avatarRoot, new Vector3(0f, 1.05f, 0f));
            _spine = CreateBone("Spine", _hips, new Vector3(0f, 0.20f, 0f));
            _chest = CreateBone("Chest", _spine, new Vector3(0f, 0.22f, 0f));

            _leftUpperArm = CreateBone("LeftUpperArm", _chest, new Vector3(-0.31f, 0.02f, 0f));
            _leftLowerArm = CreateBone("LeftLowerArm", _leftUpperArm, new Vector3(-0.36f, 0f, 0f));
            _leftHand = CreateBone("LeftHand", _leftLowerArm, new Vector3(-0.30f, 0f, 0f));
            _rightUpperArm = CreateBone("RightUpperArm", _chest, new Vector3(0.31f, 0.02f, 0f));
            _rightLowerArm = CreateBone("RightLowerArm", _rightUpperArm, new Vector3(0.36f, 0f, 0f));
            _rightHand = CreateBone("RightHand", _rightLowerArm, new Vector3(0.30f, 0f, 0f));

            _leftUpperLeg = CreateBone("LeftUpperLeg", _hips, new Vector3(-0.17f, -0.45f, 0f));
            _leftLowerLeg = CreateBone("LeftLowerLeg", _leftUpperLeg, new Vector3(0f, -0.47f, 0f));
            _leftFoot = CreateBone("LeftFoot", _leftLowerLeg, new Vector3(0f, -0.12f, 0.10f));
            _rightUpperLeg = CreateBone("RightUpperLeg", _hips, new Vector3(0.17f, -0.45f, 0f));
            _rightLowerLeg = CreateBone("RightLowerLeg", _rightUpperLeg, new Vector3(0f, -0.47f, 0f));
            _rightFoot = CreateBone("RightFoot", _rightLowerLeg, new Vector3(0f, -0.12f, 0.10f));

            CreateSegmentVisual(_hips, _spine, 0.12f, _segmentMaterial);
            CreateSegmentVisual(_spine, _chest, 0.14f, _segmentMaterial);
            CreateSegmentVisual(_chest, _leftUpperArm, 0.10f, _segmentMaterial);
            CreateSegmentVisual(_leftUpperArm, _leftLowerArm, 0.09f, _segmentMaterial);
            CreateSegmentVisual(_leftLowerArm, _leftHand, 0.08f, _segmentMaterial);
            CreateSegmentVisual(_chest, _rightUpperArm, 0.10f, _segmentMaterial);
            CreateSegmentVisual(_rightUpperArm, _rightLowerArm, 0.09f, _segmentMaterial);
            CreateSegmentVisual(_rightLowerArm, _rightHand, 0.08f, _segmentMaterial);
            CreateSegmentVisual(_hips, _leftUpperLeg, 0.12f, _segmentMaterial);
            CreateSegmentVisual(_leftUpperLeg, _leftLowerLeg, 0.11f, _segmentMaterial);
            CreateSegmentVisual(_leftLowerLeg, _leftFoot, 0.09f, _segmentMaterial);
            CreateSegmentVisual(_hips, _rightUpperLeg, 0.12f, _segmentMaterial);
            CreateSegmentVisual(_rightUpperLeg, _rightLowerLeg, 0.11f, _segmentMaterial);
            CreateSegmentVisual(_rightLowerLeg, _rightFoot, 0.09f, _segmentMaterial);
            CreateKinematicMarkers();
        }

        private static Transform CreateBone(string boneName, Transform parent, Vector3 localPosition)
        {
            var bone = new GameObject(boneName).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            bone.localRotation = Quaternion.identity;
            bone.localScale = Vector3.one;
            return bone;
        }

        private static Material CreateSegmentMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "MotionEngineDebugRigMaterial",
                color = new Color(0.12f, 0.9f, 1f, 1f)
            };
            return material;
        }

        private void CreateKinematicMarkers()
        {
            _markerRoot = new GameObject("KinematicTargetMarkers").transform;
            _markerRoot.SetParent(transform, false);
            _targetMarkerMaterial = CreateColoredMaterial("MotionEngineKinematicTargetMaterial", new Color(1f, 0.12f, 0.85f, 1f));
            _hintMarkerMaterial = CreateColoredMaterial("MotionEngineKinematicHintMaterial", new Color(1f, 0.55f, 0.08f, 1f));

            for (var i = 0; i < CanonicalKinematicTargets.ChainCount; i++)
            {
                var chain = (CanonicalKinematicChainId)i;
                _targetMarkers[i] = CreateMarker($"{chain}_Target", _targetMarkerMaterial, 0.075f);
                _hintMarkers[i] = CreateMarker($"{chain}_BendHint", _hintMarkerMaterial, 0.055f);
            }

            ClearKinematicMarkers();
        }

        private Transform CreateMarker(string markerName, Material material, float size)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = markerName;
            marker.transform.SetParent(_markerRoot, false);
            marker.transform.localScale = Vector3.one * size;
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            return marker.transform;
        }

        private static Material CreateColoredMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            return new Material(shader)
            {
                name = materialName,
                color = color,
            };
        }

        private static void CreateSegmentVisual(Transform parent, Transform child, float thickness, Material material)
        {
            var direction = child.localPosition;
            var length = direction.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = parent.name + "_SegmentVisual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = direction * 0.5f;
            visual.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            visual.transform.localScale = new Vector3(thickness, length * 0.5f, thickness);
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private void OnDestroy()
        {
            if (_segmentMaterial != null)
            {
                Destroy(_segmentMaterial);
                _segmentMaterial = null;
            }

            if (_targetMarkerMaterial != null)
            {
                Destroy(_targetMarkerMaterial);
                _targetMarkerMaterial = null;
            }

            if (_hintMarkerMaterial != null)
            {
                Destroy(_hintMarkerMaterial);
                _hintMarkerMaterial = null;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
