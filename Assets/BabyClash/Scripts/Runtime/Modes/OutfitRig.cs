using BabyClash.Core;
using UnityEngine;

namespace BabyClash
{
    /// <summary>Puts a <see cref="BabyLook"/> on a baby: hat and face accessory on the head bone, shirt colour.</summary>
    public sealed class OutfitRig : MonoBehaviour
    {
        BabyAnimator animator;
        GameObject hat;
        GameObject face;
        string shownHat;
        string shownFace;
        string shownOutfit;
        Transform spinner;

        public static OutfitRig On(BabyController baby)
        {
            if (!baby.TryGetComponent(out OutfitRig rig))
                rig = baby.gameObject.AddComponent<OutfitRig>();
            return rig;
        }

        void Awake() => animator = GetComponentInChildren<BabyAnimator>(true);

        public void Wear(BabyLook look)
        {
            if (animator == null || ProcMesh.Toon == null)
                return;
            if (shownHat != look.hat)
            {
                shownHat = look.hat;
                if (hat != null)
                    Destroy(hat);
                hat = BuildHat(look.hat);
            }
            if (shownFace != look.face)
            {
                shownFace = look.face;
                if (face != null)
                    Destroy(face);
                face = BuildFace(look.face);
            }
            if (shownOutfit != look.outfit)
            {
                shownOutfit = look.outfit;
                animator.SetShirt(ShirtOf(look.outfit));
            }
        }

        void Update()
        {
            if (shownOutfit == "outfit.rainbow")
                animator.SetShirt(Color.HSVToRGB(Mathf.Repeat(Time.time * 0.25f, 1f), 0.65f, 1f));
            if (spinner != null)
                spinner.Rotate(0f, 420f * Time.deltaTime, 0f, Space.Self);
        }

        public static Color? ShirtOf(string outfit)
        {
            switch (outfit)
            {
                case "outfit.mint": return new Color(0.5f, 0.9f, 0.7f);
                case "outfit.lilac": return new Color(0.78f, 0.65f, 1f);
                case "outfit.sunny": return new Color(1f, 0.85f, 0.3f);
                case "outfit.bubblegum": return new Color(1f, 0.55f, 0.78f);
                case "outfit.midnight": return new Color(0.2f, 0.22f, 0.45f);
                case "outfit.gold": return new Color(1f, 0.74f, 0.15f);
                case "outfit.rainbow": return Color.red;
                default: return null;
            }
        }

        // Head bone space: the skull is roughly a 0.34 m ball centred at (0, 0.2, 0); its top is at y = 0.56.
        GameObject BuildHat(string id)
        {
            Transform head = animator.Head;
            var m = new ProcMesh();
            spinner = null;
            switch (id)
            {
                case "hat.party":
                    m.Cone(new Vector3(0f, 0.48f, 0f), 0.2f, 0.42f, new Color(1f, 0.45f, 0.65f)).Torus(new Vector3(0f, 0.62f, 0f), 0.135f, 0.022f, new Color(1f, 0.9f, 0.3f))
                        .Torus(new Vector3(0f, 0.74f, 0f), 0.08f, 0.02f, new Color(0.4f, 0.8f, 1f)).Sphere(new Vector3(0f, 0.91f, 0f), 0.06f, new Color(1f, 0.9f, 0.3f));
                    break;
                case "hat.beanie":
                    m.Lathe(new Vector3(0f, 0.34f, 0f), new Color(0.35f, 0.65f, 0.95f), new Vector2(0.335f, 0f), new Vector2(0.33f, 0.1f), new Vector2(0.25f, 0.22f), new Vector2(0.12f, 0.28f), new Vector2(0f, 0.3f))
                        .Torus(new Vector3(0f, 0.36f, 0f), 0.325f, 0.045f, Color.white).Sphere(new Vector3(0f, 0.68f, 0f), 0.1f, Color.white);
                    break;
                case "hat.flower":
                    m.Torus(new Vector3(0f, 0.42f, 0f), 0.3f, 0.028f, new Color(0.45f, 0.75f, 0.4f));
                    Color[] petals = { new Color(1f, 0.5f, 0.65f), Color.white, new Color(1f, 0.8f, 0.3f), new Color(0.75f, 0.6f, 1f) };
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        Vector3 at = new Vector3(Mathf.Cos(a) * 0.31f, 0.44f, Mathf.Sin(a) * 0.31f);
                        for (int p = 0; p < 5; p++)
                            m.Sphere(at + new Vector3(Mathf.Cos(p * 1.257f) * 0.045f, 0.02f, Mathf.Sin(p * 1.257f) * 0.045f), 0.035f, petals[i % 4]);
                        m.Sphere(at + Vector3.up * 0.04f, 0.03f, new Color(1f, 0.75f, 0.2f));
                    }
                    break;
                case "hat.bunny":
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Quaternion tilt = Quaternion.Euler(0f, 0f, -14f * side);
                        m.Ellipsoid(new Vector3(0.13f * side, 0.74f, 0f), new Vector3(0.08f, 0.26f, 0.05f), Color.white, tilt)
                            .Ellipsoid(new Vector3(0.13f * side, 0.74f, 0.03f), new Vector3(0.045f, 0.2f, 0.035f), new Color(1f, 0.65f, 0.75f), tilt);
                    }
                    m.Torus(new Vector3(0f, 0.2f, 0f), 0.36f, 0.025f, new Color(1f, 0.65f, 0.75f), Quaternion.Euler(90f, 0f, 0f));
                    break;
                case "hat.chef":
                    m.Cylinder(new Vector3(0f, 0.44f, 0f), 0.24f, 0.18f, Color.white);
                    for (int i = 0; i < 6; i++)
                        m.Sphere(new Vector3(Mathf.Cos(i * 1.047f) * 0.17f, 0.72f, Mathf.Sin(i * 1.047f) * 0.17f), 0.15f, Color.white);
                    m.Sphere(new Vector3(0f, 0.8f, 0f), 0.16f, Color.white);
                    break;
                case "hat.propeller":
                    m.Lathe(new Vector3(0f, 0.36f, 0f), new Color(1f, 0.4f, 0.35f), new Vector2(0.33f, 0f), new Vector2(0.3f, 0.12f), new Vector2(0.18f, 0.22f), new Vector2(0f, 0.26f))
                        .Ellipsoid(new Vector3(0f, 0.37f, 0.33f), new Vector3(0.2f, 0.025f, 0.16f), new Color(0.35f, 0.6f, 1f)).Cylinder(new Vector3(0f, 0.6f, 0f), 0.025f, 0.12f, new Color(1f, 0.85f, 0.3f));
                    break;
                case "hat.pirate":
                    Color black = new Color(0.16f, 0.14f, 0.2f);
                    m.Lathe(new Vector3(0f, 0.4f, 0f), black, new Vector2(0.31f, 0f), new Vector2(0.27f, 0.14f), new Vector2(0.15f, 0.24f), new Vector2(0f, 0.26f))
                        .Ellipsoid(new Vector3(0f, 0.5f, 0.2f), new Vector3(0.42f, 0.17f, 0.05f), black, Quaternion.Euler(-14f, 0f, 0f))
                        .Ellipsoid(new Vector3(0f, 0.5f, -0.2f), new Vector3(0.38f, 0.15f, 0.05f), black, Quaternion.Euler(14f, 0f, 0f))
                        .Sphere(new Vector3(0f, 0.53f, 0.24f), 0.07f, Color.white).Box(new Vector3(0f, 0.46f, 0.26f), new Vector3(0.07f, 0.05f, 0.03f), Color.white);
                    break;
                case "hat.crown":
                    Color gold = new Color(1f, 0.8f, 0.2f);
                    m.Lathe(new Vector3(0f, 0.42f, 0f), gold, new Vector2(0.26f, 0f), new Vector2(0.28f, 0f), new Vector2(0.29f, 0.14f), new Vector2(0.26f, 0.14f), new Vector2(0.26f, 0f));
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 at = new Vector3(Mathf.Cos(i * 1.047f) * 0.275f, 0.54f, Mathf.Sin(i * 1.047f) * 0.275f);
                        m.Cone(at, 0.07f, 0.16f, gold).Sphere(at + Vector3.up * 0.17f, 0.035f, i % 2 == 0 ? new Color(1f, 0.3f, 0.4f) : new Color(0.3f, 0.7f, 1f));
                    }
                    break;
                case "hat.unicorn":
                    m.Lathe(new Vector3(0f, 0.46f, 0.14f), Quaternion.Euler(22f, 0f, 0f), Vector3.one, new Color(1f, 0.92f, 0.75f), 16, new Vector2(0.1f, 0f), new Vector2(0.05f, 0.25f), new Vector2(0f, 0.48f));
                    for (int i = 0; i < 4; i++)
                        m.Torus(new Vector3(0f, 0.54f + i * 0.085f, 0.175f + i * 0.034f), 0.085f - i * 0.018f, 0.012f, new Color(1f, 0.6f, 0.8f), Quaternion.Euler(32f, 0f, 0f));
                    for (int side = -1; side <= 1; side += 2)
                        m.Cone(new Vector3(0.2f * side, 0.46f, 0f), 0.08f, 0.17f, new Color(1f, 0.8f, 0.9f));
                    break;
                default:
                    return null;
            }
            GameObject go = m.Spawn("Hat", head, Vector3.zero);
            if (id == "hat.propeller")
            {
                spinner = new ProcMesh().Ellipsoid(new Vector3(0.15f, 0f, 0f), new Vector3(0.15f, 0.015f, 0.05f), new Color(1f, 0.85f, 0.3f))
                    .Ellipsoid(new Vector3(-0.15f, 0f, 0f), new Vector3(0.15f, 0.015f, 0.05f), new Color(0.35f, 0.8f, 0.5f)).Spawn("Propeller", go.transform, new Vector3(0f, 0.73f, 0f)).transform;
            }
            return go;
        }

        GameObject BuildFace(string id)
        {
            Transform head = animator.Head;
            var m = new ProcMesh();
            Color dark = new Color(0.14f, 0.12f, 0.16f);
            switch (id)
            {
                case "face.pacifier":
                    m.Ellipsoid(new Vector3(0f, 0.06f, 0.31f), new Vector3(0.085f, 0.06f, 0.025f), new Color(0.4f, 0.75f, 1f)).Sphere(new Vector3(0f, 0.06f, 0.335f), 0.03f, Color.white)
                        .Torus(new Vector3(0f, 0.035f, 0.35f), 0.045f, 0.012f, new Color(1f, 0.85f, 0.4f), Quaternion.Euler(80f, 0f, 0f));
                    break;
                case "face.bow":
                    for (int side = -1; side <= 1; side += 2)
                        m.Ellipsoid(new Vector3(0.12f * side, 0.53f, 0.12f), new Vector3(0.12f, 0.09f, 0.045f), new Color(1f, 0.45f, 0.65f), Quaternion.Euler(0f, 0f, 18f * side));
                    m.Sphere(new Vector3(0f, 0.53f, 0.14f), 0.05f, new Color(1f, 0.65f, 0.8f));
                    break;
                case "face.glasses":
                case "face.shades":
                    bool shades = id == "face.shades";
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 at = new Vector3(0.125f * side, 0.2f, 0.335f);
                        if (shades)
                            m.Ellipsoid(at, new Vector3(0.105f, 0.08f, 0.02f), dark);
                        else
                            m.Torus(at, 0.095f, 0.015f, new Color(0.85f, 0.35f, 0.3f), Quaternion.Euler(90f, 0f, 0f));
                        m.Box(new Vector3(0.265f * side, 0.215f, 0.2f), new Vector3(0.02f, 0.025f, 0.26f), shades ? dark : new Color(0.85f, 0.35f, 0.3f), Quaternion.Euler(0f, -14f * side, 0f));
                    }
                    m.Box(new Vector3(0f, 0.215f, 0.345f), new Vector3(0.07f, 0.022f, 0.02f), shades ? dark : new Color(0.85f, 0.35f, 0.3f));
                    break;
                case "face.moustache":
                    for (int side = -1; side <= 1; side += 2)
                        m.Ellipsoid(new Vector3(0.065f * side, 0.095f, 0.325f), new Vector3(0.08f, 0.032f, 0.025f), new Color(0.3f, 0.2f, 0.14f), Quaternion.Euler(0f, 0f, -14f * side))
                            .Sphere(new Vector3(0.145f * side, 0.115f, 0.3f), 0.026f, new Color(0.3f, 0.2f, 0.14f));
                    break;
                default:
                    return null;
            }
            return m.Spawn("FaceItem", head, Vector3.zero);
        }
    }
}
