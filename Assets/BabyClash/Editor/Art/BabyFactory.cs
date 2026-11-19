using System.Collections.Generic;
using BabyClash.EditorTools.Meshing;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace BabyClash.EditorTools.Art
{
    /// <summary>
    /// Builds the baby prefab: one smooth skinned body sculpted from blended shapes (painted with a player-colour
    /// shirt, blush and collar), a face rig (eyes, brows, mouths), two hair styles, effects, and the gameplay root.
    /// Model space: feet at the origin, facing +Z, about 1.4 m tall.
    /// </summary>
    public static class BabyFactory
    {
        const int Hips = 0, Chest = 1, Head = 2, ArmLeft = 3, ArmRight = 4, LegLeft = 5, LegRight = 6;
        static readonly string[] BoneNames = { "Hips", "Chest", "Head", "ArmL", "ArmR", "LegL", "LegR" };
        static readonly int[] BoneParents = { -1, Hips, Chest, Chest, Chest, Hips, Hips };
        static readonly Vector3[] BonePositions =
        {
            new Vector3(0f, 0.4f, 0f), new Vector3(0f, 0.6f, 0f), new Vector3(0f, 0.8f, 0f),
            new Vector3(-0.2f, 0.72f, 0f), new Vector3(0.2f, 0.72f, 0f),
            new Vector3(-0.11f, 0.36f, 0f), new Vector3(0.11f, 0.36f, 0f),
        };

        static readonly Color Skin = new Color(1f, 0.8f, 0.68f);
        static readonly Color Blush = new Color(1f, 0.6f, 0.6f);
        static readonly Color Nose = new Color(1f, 0.72f, 0.64f);
        static readonly Color ShirtAccent = new Color(1f, 1f, 1f, 0f); // alpha 0 = player colour
        const float SkinningSoftness = 0.022f;

        public static SdfModel Sculpt()
        {
            var m = new SdfModel { BaseColor = Skin };

            // Body: round belly and chest blend into the head with no real neck.
            m.Add(new SdfSphere(new Vector3(0f, 0.43f, 0.01f), 0.215f), bone: Hips);
            m.Add(new SdfSphere(new Vector3(0f, 0.47f, 0.07f), 0.17f), SdfOp.Union, 0.08f, bone: Hips);
            m.Add(new SdfEllipsoid(new Vector3(0f, 0.61f, 0f), new Vector3(0.2f, 0.19f, 0.17f)), SdfOp.Union, 0.1f, bone: Chest);

            // Head: skull, chubby cheeks, soft chin, button nose, little ears.
            m.Add(new SdfEllipsoid(new Vector3(0f, 1f, -0.01f), new Vector3(0.33f, 0.31f, 0.3f)), SdfOp.Union, 0.07f, bone: Head);
            for (int side = -1; side <= 1; side += 2)
                m.Add(new SdfSphere(new Vector3(0.15f * side, 0.9f, 0.15f), 0.12f), SdfOp.Union, 0.07f, bone: Head);
            m.Add(new SdfSphere(new Vector3(0f, 0.86f, 0.08f), 0.14f), SdfOp.Union, 0.08f, bone: Head);
            m.Add(new SdfSphere(new Vector3(0f, 0.955f, 0.285f), 0.035f), SdfOp.Union, 0.025f, Nose, Head);
            for (int side = -1; side <= 1; side += 2)
                m.Add(new SdfEllipsoid(new Vector3(0.31f * side, 0.98f, -0.02f), new Vector3(0.045f, 0.07f, 0.055f)), SdfOp.Union, 0.03f, Nose, Head);

            // Stubby arms with mitten hands.
            for (int side = -1; side <= 1; side += 2)
            {
                int bone = side < 0 ? ArmLeft : ArmRight;
                m.Add(new SdfTaperedCapsule(new Vector3(0.19f * side, 0.7f, 0f), new Vector3(0.33f * side, 0.5f, 0.03f), 0.075f, 0.062f), SdfOp.Union, 0.05f, bone: bone);
                m.Add(new SdfSphere(new Vector3(0.35f * side, 0.46f, 0.04f), 0.068f), SdfOp.Union, 0.03f, bone: bone);
            }

            // Chunky legs and feet.
            for (int side = -1; side <= 1; side += 2)
            {
                int bone = side < 0 ? LegLeft : LegRight;
                m.Add(new SdfTaperedCapsule(new Vector3(0.11f * side, 0.36f, 0f), new Vector3(0.12f * side, 0.1f, 0f), 0.105f, 0.078f), SdfOp.Union, 0.06f, bone: bone);
                m.Add(new SdfEllipsoid(new Vector3(0.12f * side, 0.055f, 0.045f), new Vector3(0.075f, 0.055f, 0.11f)), SdfOp.Union, 0.04f, bone: bone);
            }

            // Paint: player-coloured shirt with short sleeves, a white collar, blush and a belly button.
            var shirtVolume = new SdfEllipsoid(new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.2f, 0.34f));
            m.Add(new SdfCustom(p => Mathf.Max(shirtVolume.Distance(p), Mathf.Max(p.y - 0.755f, 0.52f - p.y))), SdfOp.Paint, 0.012f, ShirtAccent);
            m.Add(new SdfTorus(new Vector3(0f, 0.74f, 0.01f), 0.16f, 0.028f), SdfOp.Paint, 0.01f, Color.white);
            for (int side = -1; side <= 1; side += 2)
                m.Add(new SdfSphere(new Vector3(0.165f * side, 0.915f, 0.23f), 0.055f), SdfOp.Paint, 0.05f, Blush);
            m.Add(new SdfSphere(new Vector3(0f, 0.445f, 0.235f), 0.012f), SdfOp.Paint, 0.01f, Skin * 0.8f);
            return m;
        }

        /// <summary>Builds (or rebuilds) the baby prefab asset.</summary>
        public static GameObject BuildPrefab(ArtKit kit, BabyConfig config, CpuConfig cpu, InputActionAsset controls, string path)
        {
            var root = new GameObject("Baby");
            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.7f, 0f);
            capsule.radius = 0.36f;
            capsule.height = 1.4f;
            var frictionless = new PhysicsMaterial("BabyBody")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            capsule.sharedMaterial = SavePhysicsMaterial(frictionless);

            // --- Visual rig
            Transform visual = GeneratedAssets.Child("Visual", root.transform, Vector3.zero);
            Transform rig = GeneratedAssets.Child("Rig", visual, Vector3.zero);
            var bones = new Transform[BoneNames.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                Transform parent = BoneParents[i] < 0 ? rig : bones[BoneParents[i]];
                bones[i] = GeneratedAssets.Child(BoneNames[i], parent, BonePositions[i] - (BoneParents[i] < 0 ? Vector3.zero : BonePositions[BoneParents[i]]));
            }

            Mesh bodyMesh = GeneratedAssets.SaveMesh(BuildSkinnedBody(bones, rig), "BabyBody");
            var skinObject = new GameObject("Body");
            skinObject.transform.SetParent(rig, false);
            var skin = skinObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = bodyMesh;
            skin.bones = bones;
            skin.rootBone = bones[Hips];
            skin.sharedMaterial = kit.BabySkin;
            skin.localBounds = new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 1.8f, 1.6f));
            // The mesh never uses more than 4 weights per vertex; this keeps Unity on its fast skinning path.
            skin.quality = SkinQuality.Bone4;
            skin.skinnedMotionVectors = false;

            BabyFace face = BuildFace(kit, bones[Head]);
            var hair = new List<Renderer>(BuildHair(kit, bones[Head]));
            foreach (MeshRenderer brow in face.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (brow.name == "BrowMesh")
                    hair.Add(brow);
            }
            ParticleSystem tears = BuildTears(kit, bones[Head]);
            Transform stars = BuildDizzyStars(kit, bones[Head]);

            Mesh ringMesh = GeneratedAssets.SaveMesh(Shapes.Torus(0.46f, 0.035f, Color.white, 40, 8).ToMesh("PlayerRing"), "PlayerRing");
            GameObject ring = GeneratedAssets.MeshObject("PlayerRing", visual, ringMesh, kit.PlayerRing, new Vector3(0f, 0.03f, 0f),
                null, new Vector3(1f, 0.35f, 1f), false);

            // --- Gameplay
            Transform diaperSocket = GeneratedAssets.Child("DiaperSocket", bones[Hips], new Vector3(0f, -0.07f, 0f));
            Transform holdSocket = GeneratedAssets.Child("HoldSocket", bones[Head], new Vector3(0f, 0.72f, 0.02f));

            root.AddComponent<BabyMovement>();
            root.AddComponent<BabyDiaperSlot>().Configure(diaperSocket);
            root.AddComponent<BabyHands>().Configure(holdSocket);
            var controller = root.AddComponent<BabyController>();
            controller.Configure(config, 0, "BABY 1", new Color(0.3f, 0.6f, 1f));
            root.AddComponent<BabyInputHandler>().Configure(controls, BabyInputHandler.KeyboardLeftScheme, 0);
            var brain = root.AddComponent<BabyBrain>();
            brain.Configure(cpu, null, CpuDifficulty.Normal);
            brain.enabled = false;

            var animator = visual.gameObject.AddComponent<BabyAnimator>();
            animator.Configure(controller, face, rig, bones[Hips], bones[Chest], bones[Head], bones[ArmLeft], bones[ArmRight],
                bones[LegLeft], bones[LegRight], skin, hair.ToArray(), ring.GetComponent<Renderer>(), tears, stars);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Mesh BuildSkinnedBody(Transform[] bones, Transform rig)
        {
            SdfModel model = Sculpt();
            var bounds = new Bounds(new Vector3(0f, 0.7f, 0.02f), new Vector3(0.9f, 1.42f, 0.76f));
            MeshBuilder builder = model.Polygonize(bounds, 0.0125f);

            // Soft skin weights from each bone's own shapes: nearest bones win, joints blend.
            var distances = new float[bones.Length];
            foreach (Vector3 vertex in builder.Vertices)
            {
                float nearest = float.MaxValue;
                for (int b = 0; b < bones.Length; b++)
                {
                    distances[b] = model.BoneDistance(vertex, b);
                    nearest = Mathf.Min(nearest, distances[b]);
                }
                builder.BoneWeights.Add(TopFourWeights(distances, nearest));
            }

            Mesh mesh = builder.ToMesh("BabyBody");
            var bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindPoses[i] = bones[i].worldToLocalMatrix * rig.localToWorldMatrix;
            mesh.bindposes = bindPoses;
            return mesh;
        }

        static BoneWeight TopFourWeights(float[] distances, float nearest)
        {
            var weights = new List<(int bone, float weight)>(distances.Length);
            for (int b = 0; b < distances.Length; b++)
            {
                if (distances[b] < float.MaxValue)
                    weights.Add((b, Mathf.Exp(-(distances[b] - nearest) / SkinningSoftness)));
            }
            weights.Sort((a, c) => c.weight.CompareTo(a.weight));
            float total = 0f;
            for (int i = 0; i < Mathf.Min(4, weights.Count); i++)
                total += weights[i].weight;

            var result = new BoneWeight();
            for (int i = 0; i < Mathf.Min(4, weights.Count); i++)
            {
                float w = weights[i].weight / total;
                switch (i)
                {
                    case 0: result.boneIndex0 = weights[i].bone; result.weight0 = w; break;
                    case 1: result.boneIndex1 = weights[i].bone; result.weight1 = w; break;
                    case 2: result.boneIndex2 = weights[i].bone; result.weight2 = w; break;
                    default: result.boneIndex3 = weights[i].bone; result.weight3 = w; break;
                }
            }
            return result;
        }

        static BabyFace BuildFace(ArtKit kit, Transform head)
        {
            // Positions are relative to the head bone (0, 0.8, 0).
            Transform faceRoot = GeneratedAssets.Child("Face", head, Vector3.zero);
            Transform[] eyes = new Transform[2];
            Transform[] pupils = new Transform[2];
            Transform[] brows = new Transform[2];
            Mesh browMesh = GeneratedAssets.SaveMesh(Shapes.Arc(0.09f, 0.012f, 65f, 115f, Color.white, 10).ToMesh("Brow"), "Brow");
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                eyes[i] = GeneratedAssets.Child(i == 0 ? "EyeL" : "EyeR", faceRoot, new Vector3(0.105f * side, 0.22f, 0.262f));
                eyes[i].localRotation = Quaternion.Euler(0f, 13f * side, 0f);
                GeneratedAssets.MeshObject("Sclera", eyes[i], kit.UnitSphere, kit.EyeWhite, Vector3.zero, null, new Vector3(0.124f, 0.156f, 0.056f), false);
                pupils[i] = GeneratedAssets.Child("Pupil", eyes[i], new Vector3(0f, -0.008f, 0.02f));
                GeneratedAssets.MeshObject("Iris", pupils[i], kit.UnitSphere, kit.EyeDark, Vector3.zero, null, new Vector3(0.086f, 0.104f, 0.034f), false);
                GeneratedAssets.MeshObject("Shine", pupils[i], kit.UnitSphere, kit.EyeShine, new Vector3(-0.016f, 0.024f, 0.016f), null, Vector3.one * 0.026f, false);
                GeneratedAssets.MeshObject("ShineSmall", pupils[i], kit.UnitSphere, kit.EyeShine, new Vector3(0.014f, -0.016f, 0.016f), null, Vector3.one * 0.012f, false);

                brows[i] = GeneratedAssets.Child(i == 0 ? "BrowL" : "BrowR", faceRoot, new Vector3(0.105f * side, 0.325f, 0.262f));
                brows[i].localRotation = Quaternion.Euler(0f, 13f * side, 0f);
                GeneratedAssets.MeshObject("BrowMesh", brows[i], browMesh, kit.Hair, new Vector3(0f, -0.09f, 0f), null, null, false);
            }

            Transform mouth = GeneratedAssets.Child("Mouth", faceRoot, new Vector3(0f, 0.085f, 0.275f));
            Mesh smileMesh = GeneratedAssets.SaveMesh(Shapes.Arc(0.048f, 0.011f, 205f, 335f, Color.white, 14).ToMesh("Smile"), "Smile");
            Transform smile = GeneratedAssets.MeshObject("Smile", mouth, smileMesh, kit.MouthDark, new Vector3(0f, 0.03f, 0f), null, null, false).transform;
            Transform open = GeneratedAssets.Child("Open", mouth, Vector3.zero);
            GeneratedAssets.MeshObject("OpenMesh", open, kit.UnitSphere, kit.MouthDark, Vector3.zero, null, new Vector3(0.09f, 0.07f, 0.03f), false);
            GeneratedAssets.MeshObject("Tongue", open, kit.UnitSphere, kit.Tongue, new Vector3(0f, -0.016f, 0.007f), null, new Vector3(0.055f, 0.028f, 0.02f), false);
            Mesh flatMesh = GeneratedAssets.SaveMesh(Shapes.Tube(new List<Vector3> { new Vector3(-0.03f, 0f, 0f), new Vector3(0.03f, 0.004f, 0f) }, 0.009f, 0.009f, Color.white, 8).ToMesh("MouthFlat"), "MouthFlat");
            Transform flat = GeneratedAssets.MeshObject("Flat", mouth, flatMesh, kit.MouthDark, Vector3.zero, null, null, false).transform;
            open.gameObject.SetActive(false);
            flat.gameObject.SetActive(false);

            var face = faceRoot.gameObject.AddComponent<BabyFace>();
            face.Configure(eyes[0], eyes[1], pupils[0], pupils[1], brows[0], brows[1], smile, open, flat);
            return face;
        }

        static Renderer[] BuildHair(ArtKit kit, Transform head)
        {
            // Baby 1: a single big curl. Baby 2: a little sprout tuft. The builder enables one per player.
            var curlPath = new List<Vector3>();
            var root = new Vector3(0.02f, 0.5f, 0.06f);
            Quaternion tilt = Quaternion.Euler(-30f, 0f, 0f);
            for (int i = 0; i <= 28; i++)
            {
                float t = i / 28f;
                float angle = Mathf.Lerp(-90f, 290f, t) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(0.085f, 0.035f, t);
                var loop = new Vector3(Mathf.Cos(angle) * radius, 0.085f + Mathf.Sin(angle) * radius, 0f);
                curlPath.Add(root + tilt * loop);
            }
            Mesh curlMesh = GeneratedAssets.SaveMesh(Shapes.Tube(curlPath, 0.034f, 0.014f, Color.white, 10).ToMesh("HairCurl"), "HairCurl");
            GameObject curl = GeneratedAssets.MeshObject("HairCurl", head, curlMesh, kit.Hair, Vector3.zero);

            var tuft = new MeshBuilder();
            Vector3[] tips = { new Vector3(-0.06f, 0.66f, 0.04f), new Vector3(0f, 0.69f, -0.01f), new Vector3(0.06f, 0.65f, 0.03f) };
            foreach (Vector3 tip in tips)
            {
                Vector3 start = new Vector3(tip.x * 0.4f, 0.495f, tip.z * 0.5f);
                var path = new List<Vector3> { start, Vector3.Lerp(start, tip, 0.5f) + new Vector3(tip.x * 0.2f, 0f, 0.015f), tip };
                tuft.Append(Shapes.Tube(path, 0.034f, 0.008f, Color.white, 10), Matrix4x4.identity);
            }
            Mesh tuftMesh = GeneratedAssets.SaveMesh(tuft.ToMesh("HairTuft"), "HairTuft");
            GameObject sprout = GeneratedAssets.MeshObject("HairTuft", head, tuftMesh, kit.Hair, Vector3.zero);
            sprout.SetActive(false);
            return new[] { curl.GetComponent<Renderer>(), sprout.GetComponent<Renderer>() };
        }

        static ParticleSystem BuildTears(ArtKit kit, Transform head)
        {
            var root = new GameObject("Tears");
            root.transform.SetParent(head, false);
            root.transform.localPosition = new Vector3(0f, 0.2f, 0.25f);
            var system = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Particle;

            // Two cartoon tear fountains squirting up and out of the eyes.
            for (int side = -1; side <= 1; side += 2)
            {
                var jet = new GameObject(side < 0 ? "JetL" : "JetR");
                jet.transform.SetParent(root.transform, false);
                jet.transform.localPosition = new Vector3(0.1f * side, 0.02f, 0.01f);
                jet.transform.localRotation = Quaternion.LookRotation(new Vector3(0.9f * side, 1.1f, 0.5f));
                var tears = jet.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule jetMain = tears.main;
                jetMain.loop = true;
                jetMain.playOnAwake = false;
                jetMain.startLifetime = 0.7f;
                jetMain.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
                jetMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
                jetMain.startColor = new Color(0.45f, 0.78f, 1f);
                jetMain.gravityModifier = 1.3f;
                jetMain.simulationSpace = ParticleSystemSimulationSpace.World;
                jetMain.maxParticles = 150;
                ParticleSystem.EmissionModule jetEmission = tears.emission;
                jetEmission.rateOverTime = 34f;
                ParticleSystem.ShapeModule shape = tears.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 14f;
                shape.radius = 0.02f;
                jet.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Particle;
            }
            return system;
        }

        static Transform BuildDizzyStars(ArtKit kit, Transform head)
        {
            Transform stars = GeneratedAssets.Child("DizzyStars", head, new Vector3(0f, 0.62f, 0f));
            Mesh starMesh = GeneratedAssets.SaveMesh(Shapes.Star(0.07f, 0.032f, 0.035f, Color.white).ToMesh("DizzyStar"), "DizzyStar");
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                GeneratedAssets.MeshObject("Star", stars, starMesh, kit.StarGold, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.27f,
                    Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f), null, false);
            }
            stars.gameObject.SetActive(false); // shown while stunned
            return stars;
        }

        static PhysicsMaterial SavePhysicsMaterial(PhysicsMaterial material)
        {
            string path = $"{GeneratedAssets.Generated}/{material.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            EditorUtility.CopySerialized(material, existing);
            return existing;
        }

        /// <summary>Applies per-player looks on a scene instance (hair style, skin tone, shirt colour).</summary>
        public static void ApplyLooks(GameObject babyInstance, int playerIndex, Color skinTone, Color hairColor)
        {
            Transform head = babyInstance.transform.Find("Visual/Rig/Hips/Chest/Head");
            head.Find("HairCurl").gameObject.SetActive(playerIndex == 0);
            head.Find("HairTuft").gameObject.SetActive(playerIndex != 0);
            var animator = babyInstance.GetComponentInChildren<BabyAnimator>();
            animator.ConfigureLooks(skinTone, hairColor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(head.Find("HairCurl").gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(head.Find("HairTuft").gameObject);
        }
    }
}
