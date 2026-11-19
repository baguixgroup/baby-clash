using System;
using System.Collections;
using System.IO;
using BabyClash.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BabyClash.Tests
{
    /// <summary>
    /// Not a regular test: plays the real Arena scene through its menus (CPU vs CPU) and saves screenshots with the UI,
    /// for art/UX review and store shots. Run explicitly:
    /// Unity -batchmode -runTests -testPlatform PlayMode -testFilter ShowcaseCapture -shotDir /path
    /// </summary>
    [Explicit, Category("Showcase")]
    public sealed class ShowcaseCapture
    {
        const int Width = 1600;
        const int Height = 900;
        string folder;
        Camera camera;

        [UnityTest]
        public IEnumerator PlayAndCapture()
        {
            folder = ShotFolder();
            Directory.CreateDirectory(folder);
            SceneManager.LoadScene("Arena");
            yield return null;
            // The loading screen switches the scene camera off for its first frames.
            var flow = Object.FindAnyObjectByType<GameFlow>();
            float loadingEnd = Time.realtimeSinceStartup + 60f;
            while (!flow.IsReady && Time.realtimeSinceStartup < loadingEnd)
                yield return null;
            Assert.IsTrue(flow.IsReady, "The loading screen never finished.");

            camera = Camera.main;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>())
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            var round = Object.FindAnyObjectByType<RoundManager>();

            yield return new WaitForSeconds(4f);
            Capture("01_title");
            Click("HowToPlay");
            yield return new WaitForSeconds(0.5f);
            Capture("02_how_to_play");
            Click("Back");
            Click("OnePlayer");
            yield return new WaitForSeconds(0.5f);
            Capture("03_difficulty");
            Click("Normal");

            // Let the CPU play both babies.
            foreach (BabyInputHandler input in Object.FindObjectsByType<BabyInputHandler>())
                input.enabled = false;
            foreach (BabyBrain brain in Object.FindObjectsByType<BabyBrain>())
                brain.enabled = true;

            yield return new WaitForSeconds(0.6f);
            Capture("04_countdown");

            // Drop a toy by each baby so the CPUs grab one (for the "toy on the head" shot).
            ToySpawner spawner = round.Toys;
            for (int i = 0; i < 2 && i < spawner.Pool.Count; i++)
                spawner.SpawnAt(spawner.Pool[i], round.Babies[i].transform.position + Vector3.forward * 0.8f);

            bool shotHold = false, shotDash = false, shotThrow = false;
            float playEnd = Time.time + 24f;
            float nextWide = Time.time + 3f;
            int wide = 0;
            while (Time.time < playEnd && round.Phase != RoundPhase.RoundOver)
            {
                foreach (BabyController baby in round.Babies)
                {
                    if (!shotHold && baby.Hands.IsHolding)
                    {
                        yield return new WaitForSeconds(0.35f);
                        CaptureCloseup(baby, "06_closeup_holding_toy");
                        shotHold = true;
                    }
                    else if (!shotDash && baby.Status.IsDashing)
                    {
                        CaptureCloseup(baby, "06_closeup_dash");
                        shotDash = true;
                    }
                    else if (!shotThrow && baby.Status.Activity == BabyActivity.Throwing)
                    {
                        CaptureCloseup(baby, "06_closeup_throw");
                        shotThrow = true;
                    }
                }
                if (Time.time >= nextWide)
                {
                    nextWide = Time.time + 3f;
                    Capture($"05_play_{wide++}");
                }
                yield return null;
            }

            // Knock someone out for the crying/celebration and round result shots.
            BabyController loser = round.Babies[1];
            yield return WaitForPhase(round, RoundPhase.Playing, 8f);
            while (!loser.IsCrying && round.Phase == RoundPhase.Playing)
            {
                loser.ReceiveHit(new HitRequest(1000f, 6f, 1f), Vector3.right, loser.transform.position, true);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.4f);
            Capture("07_knockout");
            yield return new WaitForSecondsRealtime(0.8f);
            CaptureCloseup(loser, "07_closeup_crying");
            CaptureCloseup(round.Babies[0], "07_closeup_celebrating");
            yield return new WaitForSecondsRealtime(1.2f);
            Capture("08_round_result");

            // Finish the match quickly and show the results screen.
            for (int i = 0; i < 3 && !round.IsMatchOver; i++)
            {
                yield return WaitForPhase(round, RoundPhase.Playing, 8f);
                BabyController target = round.Babies[1];
                while (!target.IsCrying && round.Phase == RoundPhase.Playing)
                {
                    target.ReceiveHit(new HitRequest(1000f, 6f, 1f), Vector3.right, target.transform.position, true);
                    yield return null;
                }
            }
            yield return new WaitForSecondsRealtime(3.5f);
            Capture("09_results");
            Assert.IsTrue(round.IsMatchOver);
        }

        static IEnumerator WaitForPhase(RoundManager round, RoundPhase phase, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (round.Phase != phase && Time.realtimeSinceStartup < end)
                yield return null;
        }

        static void Click(string buttonName)
        {
            foreach (Button button in Object.FindObjectsByType<Button>())
            {
                if (button.name == buttonName && button.gameObject.activeInHierarchy)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
            Debug.LogWarning($"[Showcase] No active button named {buttonName}");
        }

        void CaptureCloseup(BabyController baby, string name)
        {
            var rig = camera.GetComponent<ArenaCamera>();
            rig.enabled = false;
            Vector3 saved = camera.transform.position;
            Quaternion savedRotation = camera.transform.rotation;
            Vector3 target = baby.transform.position + Vector3.up * 0.8f;
            Vector3 view = baby.transform.forward * 2.6f + Vector3.up * 1.3f + baby.transform.right * 0.9f;
            camera.transform.SetPositionAndRotation(target + view, Quaternion.LookRotation(-view));
            Capture(name);
            camera.transform.SetPositionAndRotation(saved, savedRotation);
            rig.enabled = true;
        }

        void Capture(string name)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
        }

        static string ShotFolder()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-shotDir")
                    return args[i + 1];
            }
            return Path.GetFullPath("Screenshots/Showcase");
        }
    }
}
