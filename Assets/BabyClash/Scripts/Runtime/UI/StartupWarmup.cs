using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BabyClash
{
    /// <summary>
    /// Loading screen that hides first-use shader compilation. Metal (like D3D12 and Vulkan) compiles each shader the first
    /// time it is drawn, which freezes an older Mac for several seconds. Before the title appears this draws the nursery,
    /// the babies, every toy, trails and particle effects once, and rasterizes the UI font at every size the game uses,
    /// so none of that stalls the title screen or a match.
    /// </summary>
    public sealed class StartupWarmup : MonoBehaviour
    {
        const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 !?%-+.,:;'/()";

        [SerializeField] GameObject loadingScreen;
        [SerializeField] Image progressFill;
        [SerializeField] Camera sceneCamera;
        [SerializeField] RoundManager roundManager;

        [Tooltip("Frames drawn with the toys and effects out, so every pass (shadows, depth, colour, outlines) sees them.")]
        [SerializeField] int revealFrames = 3;

        [Tooltip("Height above the floor the warm-up toys are dropped from.")]
        [SerializeField] float toyDropHeight = 1.2f;

        public bool IsDone { get; private set; }

        /// <summary>Scene wiring (editor tooling).</summary>
        public void Configure(GameObject screen, Image fill, Camera cameraToWarm, RoundManager round)
        {
            loadingScreen = screen;
            progressFill = fill;
            sceneCamera = cameraToWarm;
            roundManager = round;
        }

        public IEnumerator Run()
        {
            if (loadingScreen != null)
                loadingScreen.SetActive(true);
            SetProgress(0.05f);

            // First show only the loading screen (the UI shader compiles quickly), so the window never sits blank.
            bool drawScene = sceneCamera != null && sceneCamera.enabled;
            if (drawScene)
                sceneCamera.enabled = false;
            yield return null;
            yield return null;

            PrewarmFonts();
            SetProgress(0.2f);
            yield return null;

            // The nursery and the babies: most of the shaders.
            if (drawScene)
                sceneCamera.enabled = true;
            yield return null;
            SetProgress(0.6f);
            yield return null;

            // Things that only appear during play.
            List<TrailRenderer> trails = DropToys();
            List<ParticleSystem> particles = EmitParticles();
            SetProgress(0.8f);
            for (int i = 0; i < revealFrames; i++)
                yield return null;

            foreach (TrailRenderer trail in trails)
            {
                trail.emitting = false;
                trail.Clear();
            }
            foreach (ParticleSystem system in particles)
                system.Clear(true);
            if (roundManager != null && roundManager.Toys != null)
                roundManager.Toys.ResetForRound();
            SetProgress(1f);
            yield return null;

            if (loadingScreen != null)
                loadingScreen.SetActive(false);
            IsDone = true;
        }

        static void PrewarmFonts()
        {
            var done = new HashSet<(Font, int, FontStyle)>();
            foreach (Text text in FindObjectsByType<Text>(FindObjectsInactive.Include))
            {
                if (text.font != null && text.font.dynamic && done.Add((text.font, text.fontSize, text.fontStyle)))
                    text.font.RequestCharactersInTexture(Glyphs, text.fontSize, text.fontStyle);
            }
        }

        List<TrailRenderer> DropToys()
        {
            var trails = new List<TrailRenderer>();
            if (roundManager == null || roundManager.Toys == null)
                return trails;
            IReadOnlyList<Toy> pool = roundManager.Toys.Pool;
            for (int i = 0; i < pool.Count; i++)
            {
                Toy toy = pool[i];
                if (toy == null)
                    continue;
                // A row across the middle of the arena, in view of the camera.
                float x = (i - (pool.Count - 1) * 0.5f) * 1.2f;
                toy.Spawn(new Vector3(x, toyDropHeight, 0f));
                foreach (TrailRenderer trail in toy.GetComponentsInChildren<TrailRenderer>())
                {
                    trail.emitting = true;
                    trails.Add(trail);
                }
            }
            return trails;
        }

        static List<ParticleSystem> EmitParticles()
        {
            var systems = new List<ParticleSystem>();
            foreach (ParticleSystem system in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
            {
                system.Emit(4);
                systems.Add(system);
            }
            return systems;
        }

        void SetProgress(float value)
        {
            if (progressFill != null)
                progressFill.fillAmount = value;
        }
    }
}
