using BabyClash.EditorTools.Meshing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabyClash.EditorTools.Art
{
    /// <summary>Sculpts the diaper and the toys, and builds their prefabs.</summary>
    public static class ItemFactory
    {
        const float CellSize = 0.011f;

        public static Mesh DiaperMesh()
        {
            // Worn around the hips: origin at the diaper's centre (the baby's diaper socket).
            var m = new SdfModel { BaseColor = new Color(0.99f, 0.98f, 0.95f) };
            m.Add(new SdfEllipsoid(Vector3.zero, new Vector3(0.29f, 0.17f, 0.26f)));
            m.Add(new SdfHalfSpace(new Vector3(0f, 0.1f, 0f), Vector3.down), SdfOp.Intersect, 0.035f);
            for (int side = -1; side <= 1; side += 2)
                m.Add(new SdfTaperedCapsule(new Vector3(0.125f * side, -0.02f, 0f), new Vector3(0.14f * side, -0.4f, 0f), 0.1f, 0.1f), SdfOp.Subtract, 0.035f);
            // Tabs.
            for (int side = -1; side <= 1; side += 2)
                m.Add(new SdfRoundBox(new Vector3(0.235f * side, 0.045f, 0.14f), new Vector3(0.045f, 0.035f, 0.03f), 0.015f,
                    Quaternion.Euler(0f, 35f * side, 0f)), SdfOp.Union, 0.02f, new Color(1f, 0.84f, 0.38f));
            // Waistband and pastel polka dots.
            m.Add(new SdfHalfSpace(new Vector3(0f, 0.06f, 0f), Vector3.up), SdfOp.Paint, 0.01f, new Color(0.62f, 0.83f, 1f));
            Color[] dots = { new Color(0.8f, 0.72f, 1f), new Color(0.65f, 0.9f, 0.75f), new Color(1f, 0.72f, 0.8f) };
            int dot = 0;
            for (int ring = 0; ring < 2; ring++)
            {
                for (int i = 0; i < 9; i++)
                {
                    float angle = (i / 9f + ring * 0.055f) * Mathf.PI * 2f;
                    float y = ring == 0 ? -0.02f : -0.09f;
                    var position = new Vector3(Mathf.Cos(angle) * 0.29f, y, Mathf.Sin(angle) * 0.26f);
                    m.Add(new SdfSphere(position, 0.026f), SdfOp.Paint, 0.008f, dots[dot++ % dots.Length]);
                }
            }
            var bounds = new Bounds(new Vector3(0f, -0.03f, 0f), new Vector3(0.66f, 0.3f, 0.6f));
            return GeneratedAssets.SaveMesh(m.Polygonize(bounds, CellSize).ToMesh("Diaper"), "Diaper");
        }

        public static Mesh BlockMesh(string name, Color body, Color panel)
        {
            var m = new SdfModel { BaseColor = body };
            m.Add(new SdfRoundBox(Vector3.zero, Vector3.one * 0.19f, 0.05f));
            // Lighter inset panel on each face, edges keep the body colour.
            m.Add(new SdfRoundBox(Vector3.zero, new Vector3(0.13f, 0.13f, 0.3f), 0.03f), SdfOp.Paint, 0.01f, panel);
            m.Add(new SdfRoundBox(Vector3.zero, new Vector3(0.3f, 0.13f, 0.13f), 0.03f), SdfOp.Paint, 0.01f, panel);
            m.Add(new SdfRoundBox(Vector3.zero, new Vector3(0.13f, 0.3f, 0.13f), 0.03f), SdfOp.Paint, 0.01f, panel);
            // A raised dot on the panels, like a toy stud.
            m.Add(new SdfSphere(new Vector3(0f, 0f, 0.19f), 0.05f), SdfOp.Union, 0.02f, body);
            m.Add(new SdfSphere(new Vector3(0f, 0f, -0.19f), 0.05f), SdfOp.Union, 0.02f, body);
            m.Add(new SdfSphere(new Vector3(0f, 0.19f, 0f), 0.05f), SdfOp.Union, 0.02f, body);
            var bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);
            return GeneratedAssets.SaveMesh(m.Polygonize(bounds, CellSize).ToMesh(name), name);
        }

        public static Mesh DuckMesh()
        {
            var yellow = new Color(1f, 0.84f, 0.2f);
            var m = new SdfModel { BaseColor = yellow };
            m.Add(new SdfEllipsoid(new Vector3(0f, -0.04f, -0.02f), new Vector3(0.16f, 0.12f, 0.2f)));
            m.Add(new SdfSphere(new Vector3(0f, 0.12f, 0.08f), 0.1f), SdfOp.Union, 0.05f);
            m.Add(new SdfSphere(new Vector3(0f, 0.04f, -0.2f), 0.055f), SdfOp.Union, 0.05f);
            for (int side = -1; side <= 1; side += 2)
                m.Add(new SdfEllipsoid(new Vector3(0.14f * side, 0f, -0.02f), new Vector3(0.04f, 0.07f, 0.11f)), SdfOp.Union, 0.03f);
            m.Add(new SdfEllipsoid(new Vector3(0f, 0.1f, 0.19f), new Vector3(0.055f, 0.022f, 0.05f)), SdfOp.Union, 0.015f, new Color(1f, 0.5f, 0.15f));
            for (int side = -1; side <= 1; side += 2)
                m.Add(new SdfSphere(new Vector3(0.05f * side, 0.155f, 0.15f), 0.018f), SdfOp.Paint, 0.005f, new Color(0.1f, 0.08f, 0.08f));
            var bounds = new Bounds(new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.4f, 0.56f));
            return GeneratedAssets.SaveMesh(m.Polygonize(bounds, CellSize).ToMesh("Duck"), "Duck");
        }

        public static Mesh BottleMesh()
        {
            var m = new SdfModel { BaseColor = new Color(0.82f, 0.92f, 1f) };
            m.Add(new SdfTaperedCapsule(new Vector3(0f, -0.1f, 0f), new Vector3(0f, 0.07f, 0f), 0.08f, 0.078f));
            m.Add(new SdfTorus(new Vector3(0f, 0.105f, 0f), 0.068f, 0.024f), SdfOp.Union, 0.015f, new Color(1f, 0.62f, 0.75f));
            m.Add(new SdfTaperedCapsule(new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.2f, 0f), 0.042f, 0.02f), SdfOp.Union, 0.02f, new Color(0.96f, 0.8f, 0.58f));
            // Milk inside, measuring marks on the side.
            m.Add(new SdfHalfSpace(new Vector3(0f, 0.01f, 0f), Vector3.down), SdfOp.Paint, 0.006f, new Color(1f, 0.99f, 0.95f));
            for (int i = 0; i < 3; i++)
                m.Add(new SdfRoundBox(new Vector3(0f, -0.07f + i * 0.045f, 0.08f), new Vector3(0.03f, 0.004f, 0.02f), 0.002f), SdfOp.Paint, 0.004f, new Color(0.35f, 0.5f, 0.85f));
            var bounds = new Bounds(new Vector3(0f, 0.03f, 0f), new Vector3(0.24f, 0.42f, 0.24f));
            return GeneratedAssets.SaveMesh(m.Polygonize(bounds, CellSize).ToMesh("Bottle"), "Bottle");
        }

        public static GameObject BuildDiaperPrefab(ArtKit kit, DiaperConfig config, Mesh mesh, string path)
        {
            GameObject root = ItemRoot("Diaper", 0.3f, out BoxCollider solid, out SphereCollider hitZone, out TrailRenderer trail, kit);
            solid.size = new Vector3(0.56f, 0.28f, 0.5f);
            solid.center = new Vector3(0f, -0.03f, 0f);
            hitZone.radius = 0.42f;
            GeneratedAssets.MeshObject("Visual", root.transform, mesh, kit.Cloth, Vector3.zero);
            root.AddComponent<Diaper>().Configure(config, solid, hitZone, trail);
            return Save(root, path);
        }

        public static GameObject BuildToyPrefab(ArtKit kit, string name, ToyConfig config, Mesh mesh, Vector3 colliderSize,
            float mass, string path)
        {
            GameObject root = ItemRoot(name, mass, out BoxCollider solid, out SphereCollider hitZone, out TrailRenderer trail, kit);
            solid.size = colliderSize;
            hitZone.radius = Mathf.Max(colliderSize.x, Mathf.Max(colliderSize.y, colliderSize.z)) * 0.5f + 0.2f;
            GameObject visual = GeneratedAssets.MeshObject("Visual", root.transform, mesh, kit.Plastic, Vector3.zero);
            root.AddComponent<Toy>().Configure(config, solid, hitZone, trail, new[] { visual.GetComponent<Renderer>() });
            return Save(root, path);
        }

        static GameObject ItemRoot(string name, float mass, out BoxCollider solid, out SphereCollider hitZone, out TrailRenderer trail, ArtKit kit)
        {
            var root = new GameObject(name);
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.angularDamping = 0.8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            solid = root.AddComponent<BoxCollider>();
            solid.sharedMaterial = BouncyMaterial();
            hitZone = root.AddComponent<SphereCollider>();
            hitZone.isTrigger = true;

            trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = kit.Trail;
            trail.time = 0.2f;
            trail.startWidth = 0.32f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.startColor = new Color(1f, 1f, 1f, 0.55f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return root;
        }

        static PhysicsMaterial BouncyMaterial()
        {
            string path = $"{GeneratedAssets.Generated}/ItemBounce.asset";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("ItemBounce");
                AssetDatabase.CreateAsset(material, path);
            }
            material.dynamicFriction = 0.8f;
            material.staticFriction = 0.8f;
            material.bounciness = 0.35f;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject Save(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
