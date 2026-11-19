using System.Collections;
using System.Reflection;
using BabyClash.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BabyClash.Tests
{
    public sealed class ProgressionFlowTests
    {
        const string ProfileKey = "BabyClash.Profile";
        string previousSave;
        bool hadSave;

        static void ReloadProfile() => typeof(PlayerProgress)
            .GetField("profile", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);

        [SetUp]
        public void SetUp()
        {
            hadSave = PlayerPrefs.HasKey(ProfileKey);
            previousSave = PlayerPrefs.GetString(ProfileKey);
            PlayerPrefs.DeleteKey(ProfileKey);
            ReloadProfile();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (hadSave)
                PlayerPrefs.SetString(ProfileKey, previousSave);
            else
                PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.Save();
            ReloadProfile();
        }

        [UnityTearDown]
        public IEnumerator CloseArena()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalMatchAwardsRewardsOnceAndPersistsThem()
        {
            yield return SceneManager.LoadSceneAsync("Arena");
            var flow = Object.FindFirstObjectByType<GameFlow>();
            var round = Object.FindFirstObjectByType<RoundManager>();
            Assert.IsNotNull(flow);
            yield return TestArena.WaitUntil(() => flow.IsReady, 45f);
            Assert.AreEqual(0, PlayerProgress.Profile.rattles, "Title demo must not earn rewards.");
            Assert.IsNotNull(GameObject.Find("Challenges/Progress"));
            Capture("progress-title.png");
            yield return null;

            flow.StartMatch(true, CpuDifficulty.Easy);
            foreach (var brain in Object.FindObjectsByType<BabyBrain>(FindObjectsSortMode.None))
                brain.enabled = false;
            Time.timeScale = 4f;
            for (int win = 0; win < round.RoundsToWin; win++)
            {
                yield return TestArena.WaitUntil(() => round.Phase == RoundPhase.Playing, 10f);
                round.Babies[1].BurstIntoTears();
                yield return null;
            }
            yield return TestArena.WaitUntil(() => PlayerProgress.Profile.lifetime.matches == 1, 15f);
            int balance = PlayerProgress.Profile.rattles;
            Assert.Greater(balance, 0);
            var detail = GameObject.Find("ResultsScreen/Panel/Detail").GetComponent<Text>();
            StringAssert.Contains("rattles", detail.text);
            Capture("progress-results.png");
            yield return null;
            ReloadProfile();
            Assert.AreEqual(balance, PlayerProgress.Profile.rattles);
            Assert.AreEqual(1, PlayerProgress.Profile.lifetime.matches);
            flow.ShowTitle();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(balance, PlayerProgress.Profile.rattles);
        }

        static void Capture(string filename)
        {
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var overlay = new System.Collections.Generic.List<Canvas>();
            var target = new RenderTexture(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                foreach (var canvas in canvases)
                {
                    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        continue;
                    overlay.Add(canvas);
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../" + filename), image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally
            {
                foreach (var canvas in overlay)
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
