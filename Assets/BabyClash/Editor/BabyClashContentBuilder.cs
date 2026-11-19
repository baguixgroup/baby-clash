using System;
using System.Collections.Generic;
using System.IO;
using BabyClash.EditorTools.Art;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BabyClash.EditorTools
{
    /// <summary>
    /// Generates all game content from code: render pipeline, balance assets (only if missing), textures, materials,
    /// sculpted meshes, prefabs, HUD icons, and the Arena scene (nursery, arena, babies, items, camera, audio, UI).
    /// Re-run it via the "Baby Clash" menu. Balance assets are never overwritten.
    /// </summary>
    public static class BabyClashContentBuilder
    {
        /// <summary>Bump when generated content changes; editors rebuild automatically on next open.</summary>
        public const int ContentVersion = 4;

        public const string ScenePath = GeneratedAssets.Scenes + "/Arena.unity";
        const string VersionMarker = GeneratedAssets.Generated + "/ContentVersion.txt";
        const string InputActionsPath = GeneratedAssets.Root + "/Input/BabyClashControls.inputactions";

        static readonly Vector2 ArenaHalfExtents = new Vector2(8f, 5f);
        const float WallThickness = 0.6f;
        const float WallColliderHeight = 4f;
        static readonly Vector3[] SpawnPositions = { new Vector3(-5.5f, 0f, 0f), new Vector3(5.5f, 0f, 0f) };
        static readonly Vector3 DiaperRecoveryPosition = new Vector3(0f, 0f, 3.2f);

        static readonly string[] PlayerNames = { "BABY 1", "BABY 2" };
        static readonly Color[] ShirtColors = { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.5f, 0.35f) };
        static readonly Color[] SkinTones = { Color.white, new Color(0.88f, 0.72f, 0.62f) };
        static readonly Color[] HairColors = { new Color(0.45f, 0.28f, 0.15f), new Color(0.16f, 0.1f, 0.08f) };
        static readonly string[] KeyboardSchemes = { BabyInputHandler.KeyboardLeftScheme, BabyInputHandler.KeyboardRightScheme };

        sealed class Balance
        {
            public BabyConfig Baby;
            public DiaperConfig Diaper;
            public MatchConfig Match;
            public CpuConfig Cpu;
            public ToyConfig Block;
            public ToyConfig Duck;
            public ToyConfig Bottle;
        }

        [MenuItem("Baby Clash/Rebuild Game Content", priority = 0)]
        static void RebuildFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog("Rebuild Baby Clash content",
                "This regenerates the art, prefabs, UI and the Arena scene.\n\n" +
                "Balance assets in Assets/BabyClash/Balance are kept. Manual edits to generated prefabs or the scene are lost.",
                "Rebuild", "Cancel");
            if (confirmed && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                BuildAll();
        }

        [MenuItem("Baby Clash/Open Arena Scene", priority = 1)]
        static void OpenArenaFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildAll();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static bool IsContentCurrent() =>
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
            && File.Exists(VersionMarker)
            && File.ReadAllText(VersionMarker).Trim() == ContentVersion.ToString();

        /// <summary>
        /// Builds everything and leaves the Arena scene open. Also usable from the command line:
        /// -executeMethod BabyClash.EditorTools.BabyClashContentBuilder.BuildAll
        /// </summary>
        public static void BuildAll()
        {
            bool asyncShaders = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false; // baked icons must see fully compiled shaders
            try
            {
                Build();
            }
            finally
            {
                EditorSettings.asyncShaderCompilation = asyncShaders;
            }
        }

        static void Build()
        {
            double started = EditorApplication.timeSinceStartup;
            GeneratedAssets.EnsureFolders();
            RemoveLegacyContent();
            RenderSetup.EnsurePipeline();
            VolumeProfile post = RenderSetup.EnsurePostProcessing();
            Balance balance = LoadOrCreateBalance();
            InputActionAsset controls = LoadControls();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArtKit kit = BuildKit();

            GameObject diaperPrefab = ItemFactory.BuildDiaperPrefab(kit, balance.Diaper, ItemFactory.DiaperMesh(), $"{GeneratedAssets.Prefabs}/Diaper.prefab");
            GameObject blockRedPrefab = ItemFactory.BuildToyPrefab(kit, "ToyBlockRed", balance.Block,
                ItemFactory.BlockMesh("ToyBlockRed", new Color(1f, 0.45f, 0.45f), new Color(1f, 0.88f, 0.7f)), Vector3.one * 0.38f, 0.5f, $"{GeneratedAssets.Prefabs}/ToyBlockRed.prefab");
            GameObject blockBluePrefab = ItemFactory.BuildToyPrefab(kit, "ToyBlockBlue", balance.Block,
                ItemFactory.BlockMesh("ToyBlockBlue", new Color(0.45f, 0.65f, 1f), new Color(0.85f, 0.93f, 1f)), Vector3.one * 0.38f, 0.5f, $"{GeneratedAssets.Prefabs}/ToyBlockBlue.prefab");
            GameObject duckPrefab = ItemFactory.BuildToyPrefab(kit, "ToyDuck", balance.Duck, ItemFactory.DuckMesh(),
                new Vector3(0.32f, 0.3f, 0.42f), 0.2f, $"{GeneratedAssets.Prefabs}/ToyDuck.prefab");
            GameObject bottlePrefab = ItemFactory.BuildToyPrefab(kit, "ToyBottle", balance.Bottle, ItemFactory.BottleMesh(),
                new Vector3(0.17f, 0.4f, 0.17f), 0.4f, $"{GeneratedAssets.Prefabs}/ToyBottle.prefab");
            GameObject babyPrefab = BabyFactory.BuildPrefab(kit, balance.Baby, balance.Cpu, controls, $"{GeneratedAssets.Prefabs}/Baby.prefab");

            BuildLighting();
            BuildScene(kit, post, balance, controls, babyPrefab, diaperPrefab,
                new[] { blockRedPrefab, blockBluePrefab, duckPrefab, duckPrefab, bottlePrefab, bottlePrefab });

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings();
            ConfigurePlayer();
            File.WriteAllText(VersionMarker, ContentVersion.ToString());
            AssetDatabase.ImportAsset(VersionMarker);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Baby Clash] Generated game content v{ContentVersion} in {EditorApplication.timeSinceStartup - started:0.0}s. Press Play!");
        }

        // ------------------------------------------------------------------ Assets

        /// <summary>Deletes content made by earlier builder versions that nothing references any more.</summary>
        static void RemoveLegacyContent()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { GeneratedAssets.Generated }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') == GeneratedAssets.Generated)
                    AssetDatabase.DeleteAsset(path);
            }
            foreach (string legacy in new[] { $"{GeneratedAssets.Generated}/SoftDot.asset", $"{GeneratedAssets.Generated}/DiaperBounce.asset" })
            {
                if (AssetDatabase.LoadMainAssetAtPath(legacy) != null)
                    AssetDatabase.DeleteAsset(legacy);
            }
        }

        static Balance LoadOrCreateBalance()
        {
            string folder = GeneratedAssets.Balance;
            return new Balance
            {
                Baby = GeneratedAssets.LoadOrCreate<BabyConfig>($"{folder}/BabyConfig.asset"),
                Diaper = GeneratedAssets.LoadOrCreate<DiaperConfig>($"{folder}/DiaperConfig.asset"),
                Match = GeneratedAssets.LoadOrCreate<MatchConfig>($"{folder}/MatchConfig.asset"),
                Cpu = GeneratedAssets.LoadOrCreate<CpuConfig>($"{folder}/CpuConfig.asset"),
                Block = LoadToy($"{folder}/ToyBlock.asset", toy =>
                {
                    toy.displayName = "Block";
                    toy.impactSound = ToySound.Clack;
                    toy.throwSpeed = 13f;
                    toy.throwArc = 3.2f;
                    toy.gravityMultiplier = 1.7f;
                    toy.spinStyle = SpinStyle.Tumble;
                    toy.spinSpeed = 600f;
                    toy.directHitCry = 12f;
                    toy.bounceHitCry = 8f;
                    toy.directHitKnockback = 7f;
                    toy.bounceHitKnockback = 4.5f;
                }),
                Duck = LoadToy($"{folder}/ToyDuck.asset", toy =>
                {
                    toy.displayName = "Rubber Duck";
                    toy.impactSound = ToySound.Squeak;
                    toy.throwSpeed = 17f;
                    toy.throwArc = 3.8f;
                    toy.gravityMultiplier = 1.5f;
                    toy.spinStyle = SpinStyle.Tumble;
                    toy.spinSpeed = 900f;
                    toy.directHitCry = 8f;
                    toy.bounceHitCry = 6f;
                    toy.directHitKnockback = 5f;
                    toy.bounceHitKnockback = 3.5f;
                    toy.landingSpeedRetention = 0.55f;
                }),
                Bottle = LoadToy($"{folder}/ToyBottle.asset", toy =>
                {
                    toy.displayName = "Baby Bottle";
                    toy.impactSound = ToySound.Slosh;
                    toy.throwSpeed = 14f;
                    toy.throwArc = 4.5f;
                    toy.gravityMultiplier = 1.8f;
                    toy.spinStyle = SpinStyle.Tumble;
                    toy.spinSpeed = 700f;
                    toy.directHitCry = 14f;
                    toy.bounceHitCry = 9f;
                    toy.directHitKnockback = 9f;
                    toy.bounceHitKnockback = 5f;
                    toy.spawnWeight = 0.7f;
                }),
            };
        }

        static ToyConfig LoadToy(string path, Action<ToyConfig> defaults)
        {
            var toy = AssetDatabase.LoadAssetAtPath<ToyConfig>(path);
            if (toy != null)
                return toy;
            toy = ScriptableObject.CreateInstance<ToyConfig>();
            defaults(toy);
            AssetDatabase.CreateAsset(toy, path);
            return toy;
        }

        static InputActionAsset LoadControls()
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (controls == null)
            {
                AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate);
                controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            }
            if (controls == null)
                Debug.LogError($"[Baby Clash] Could not load {InputActionsPath}; human players won't respond to input.");
            return controls;
        }

        static ArtKit BuildKit()
        {
            string tex = GeneratedAssets.Textures;
            Texture2D softDot = TextureFactory.SoftDot(tex);
            TextureFactory.RoundedRect(tex, 128, 44f);
            TextureFactory.Circle(tex, 128);
            TextureFactory.StarSprite(tex);
            var materials = new MaterialFactory(GeneratedAssets.Materials);
            var warmShade = new Color(0.86f, 0.62f, 0.66f);
            var softShade = new Color(0.78f, 0.7f, 0.86f);
            return new ArtKit
            {
                Factory = materials,
                BabySkin = materials.Toon("BabySkin", Color.white, 0.008f, warmShade, 0.28f, 0.08f),
                Hair = materials.Toon("BabyHair", Color.white, 0.006f, new Color(0.6f, 0.55f, 0.7f), 0.2f, 0.25f),
                EyeWhite = materials.Toon("EyeWhite", Color.white, 0f, new Color(0.9f, 0.9f, 0.96f), 0f, 0f),
                EyeDark = materials.Toon("EyeDark", new Color(0.16f, 0.1f, 0.1f), 0f, Color.white, 0f, 0.4f),
                EyeShine = materials.Toon("EyeShine", Color.white, 0f, Color.white, 0f, 0f, null, 1f, new Color(0.6f, 0.6f, 0.6f)),
                MouthDark = materials.Toon("MouthDark", new Color(0.45f, 0.12f, 0.16f), 0f, Color.white, 0f, 0f),
                Tongue = materials.Toon("Tongue", new Color(1f, 0.52f, 0.58f), 0f, Color.white, 0f, 0f),
                PlayerRing = materials.Toon("PlayerRing", Color.white, 0f, new Color(0.9f, 0.9f, 0.9f), 0f, 0f, null, 1f, new Color(0.25f, 0.25f, 0.25f)),
                StarGold = materials.Toon("StarGold", new Color(1f, 0.85f, 0.3f), 0.004f, Color.white, 0.2f, 0.3f, null, 1f, new Color(0.3f, 0.22f, 0.05f)),
                Cloth = materials.Toon("DiaperCloth", Color.white, 0.007f, softShade, 0.25f, 0.05f),
                Plastic = materials.Toon("ToyPlastic", Color.white, 0.007f, softShade, 0.25f, 0.35f),
                Floor = materials.Toon("WoodFloor", Color.white, 0f, new Color(0.72f, 0.62f, 0.7f), 0.05f, 0.05f, TextureFactory.WoodFloor(tex)),
                Mat = materials.Toon("PuzzleMat", Color.white, 0f, softShade, 0.05f, 0.02f, TextureFactory.PuzzleMat(tex)),
                Wallpaper = materials.Toon("Wallpaper", Color.white, 0f, new Color(0.8f, 0.78f, 0.86f), 0f, 0f, TextureFactory.Wallpaper(tex)),
                Trim = materials.Toon("Trim", Color.white, 0f, softShade, 0.1f, 0.1f),
                Painted = materials.Toon("PaintedWood", Color.white, 0.01f, softShade, 0.2f, 0.12f),
                Fabric = materials.Toon("Fabric", Color.white, 0.01f, softShade, 0.3f, 0f),
                Glass = materials.Toon("WindowSky", Color.white, 0f, Color.white, 0f, 0f, TextureFactory.WindowSky(tex), 1f, new Color(0.55f, 0.6f, 0.65f)),
                LampShade = materials.Toon("LampShade", new Color(1f, 0.92f, 0.75f), 0.006f, Color.white, 0f, 0f, null, 1f, new Color(1.2f, 0.95f, 0.6f)),
                PictureSun = materials.Toon("PictureSun", Color.white, 0f, Color.white, 0f, 0f, TextureFactory.PictureSun(tex)),
                PictureMoon = materials.Toon("PictureMoon", Color.white, 0f, Color.white, 0f, 0f, TextureFactory.PictureMoon(tex)),
                PictureRainbow = materials.Toon("PictureRainbow", Color.white, 0f, Color.white, 0f, 0f, TextureFactory.PictureRainbow(tex)),
                Particle = materials.Particle("SoftParticle", softDot),
                Trail = materials.Particle("ThrowTrail", softDot),
                SoftDot = softDot,
                UnitSphere = ArtKit.CreateUnitSphere(),
            };
        }

        // ------------------------------------------------------------------ Scene

        static void BuildLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.85f, 0.78f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.62f, 0.52f, 0.45f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        }

        static void BuildScene(ArtKit kit, VolumeProfile post, Balance balance, InputActionAsset controls, GameObject babyPrefab,
            GameObject diaperPrefab, GameObject[] toyPrefabs)
        {
            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = post;

            Transform world = new GameObject("World").transform;
            RoomFactory.Build(kit, ArenaHalfExtents, world);

            Transform arena = GeneratedAssets.Child("Arena", world, Vector3.zero);
            RoomFactory.BuildObstacles(kit, arena);
            BuildArenaWalls(arena);
            Transform[] spawns = BuildSpawnPoints(arena);
            Transform recoveryPoint = GeneratedAssets.Child("DiaperRecoveryPoint", arena, DiaperRecoveryPosition);
            var bounds = arena.gameObject.AddComponent<ArenaBounds>();

            // Babies and their diapers.
            var babies = new BabyController[SpawnPositions.Length];
            var inputs = new BabyInputHandler[babies.Length];
            var brains = new BabyBrain[babies.Length];
            var diapers = new Diaper[babies.Length];
            Transform diaperContainer = GeneratedAssets.Child("Diapers", world, Vector3.zero);
            for (int i = 0; i < babies.Length; i++)
            {
                var babyObject = (GameObject)PrefabUtility.InstantiatePrefab(babyPrefab);
                babyObject.name = $"Baby_{i + 1}";
                babyObject.transform.SetPositionAndRotation(spawns[i].position, spawns[i].rotation);
                babies[i] = babyObject.GetComponent<BabyController>();
                babies[i].Configure(balance.Baby, i, PlayerNames[i], ShirtColors[i]);
                inputs[i] = babyObject.GetComponent<BabyInputHandler>();
                inputs[i].Configure(controls, KeyboardSchemes[i], i);
                brains[i] = babyObject.GetComponent<BabyBrain>();
                BabyFactory.ApplyLooks(babyObject, i, SkinTones[i], HairColors[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(babyObject.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(babies[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(inputs[i]);

                var diaperObject = (GameObject)PrefabUtility.InstantiatePrefab(diaperPrefab, diaperContainer);
                diaperObject.name = $"Diaper_{i + 1:00}";
                diaperObject.transform.position = spawns[i].position + Vector3.up * 0.33f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(diaperObject.transform);
                diapers[i] = diaperObject.GetComponent<Diaper>();
            }

            // Toy pool: every toy that can ever appear, inactive until dropped in.
            Transform toyContainer = GeneratedAssets.Child("Toys", world, Vector3.zero);
            var toys = new Toy[toyPrefabs.Length];
            for (int i = 0; i < toyPrefabs.Length; i++)
            {
                var toyObject = (GameObject)PrefabUtility.InstantiatePrefab(toyPrefabs[i], toyContainer);
                toyObject.name = $"{toyPrefabs[i].name}_{i + 1}";
                toyObject.transform.position = new Vector3(0f, -10f, 0f);
                toys[i] = toyObject.GetComponent<Toy>();
                toyObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(toyObject.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(toyObject);
            }

            var items = new List<Throwable>(diapers);
            items.AddRange(toys);
            bounds.Configure(ArenaHalfExtents, recoveryPoint, babies, items.ToArray());

            // Camera.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 36f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.94f, 0.9f, 0.86f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 120f;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Quaternion view = Quaternion.Euler(48f, 0f, 0f);
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f) - view * Vector3.forward * 19f, view);
            var arenaCamera = cameraObject.AddComponent<ArenaCamera>();
            arenaCamera.Configure(Array.ConvertAll(babies, b => b.transform), Vector3.zero);

            // Game systems.
            var game = new GameObject("Game");
            var round = game.AddComponent<RoundManager>();
            var spawner = game.AddComponent<ToySpawner>();
            spawner.Configure(balance.Match, bounds, toys);
            round.Configure(balance.Match, babies, spawns, diapers, bounds, spawner, false);
            foreach (BabyBrain brain in brains)
            {
                brain.Configure(balance.Cpu, round, CpuDifficulty.Normal);
                PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
            }

            var time = game.AddComponent<TimeController>();
            var music = game.AddComponent<AudioSource>();
            var audio = game.AddComponent<AudioDirector>();
            audio.Configure(music);
            var feedback = game.AddComponent<ImpactFeedback>();
            feedback.Configure(round, arenaCamera, audio, time,
                BurstEmitter("HitStars", game.transform, AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedAssets.Textures}/UiStar.png"), kit,
                    new Color(1f, 0.92f, 0.4f), Color.white, 0.35f, 0.65f, 3f, 7f, 0.14f, 0.3f, 0.6f),
                BurstEmitter("Dust", game.transform, kit.SoftDot, kit,
                    new Color(1f, 0.95f, 0.88f, 0.8f), new Color(0.95f, 0.88f, 0.8f, 0.6f), 0.4f, 0.8f, 0.8f, 2f, 0.25f, 0.5f, -0.05f),
                BurstEmitter("Confetti", game.transform, AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedAssets.Textures}/UiRoundedRect.png"), kit,
                    new Color(1f, 0.5f, 0.6f), new Color(0.45f, 0.75f, 1f), 1.2f, 2f, 3f, 7f, 0.08f, 0.16f, 0.5f));
            game.AddComponent<DebugOverlay>().Configure(round);

            // Icons need the lit scene and the finished prefabs.
            var diaperCopy = (GameObject)PrefabUtility.InstantiatePrefab(diaperPrefab);
            Sprite diaperIcon = IconBaker.Bake("IconDiaper", diaperCopy, Vector3.zero, new Vector3(0f, 0.45f, -1f), 1.25f, 32f);
            Object.DestroyImmediate(diaperCopy);
            var toyIcons = new List<ToyIcon>();
            for (int i = 0; i < toys.Length; i++)
            {
                if (toyIcons.Exists(icon => icon.toy == toys[i].Config))
                    continue;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(toyPrefabs[i]);
                copy.SetActive(true);
                Sprite sprite = IconBaker.Bake($"Icon{toys[i].Config.name}", copy, Vector3.zero, new Vector3(0.4f, 0.6f, -1f), 1.1f, 32f);
                toyIcons.Add(new ToyIcon { toy = toys[i].Config, sprite = sprite });
                Object.DestroyImmediate(copy);
            }
            var portraits = new Sprite[babies.Length];
            for (int i = 0; i < babies.Length; i++)
                portraits[i] = BakePortrait(babies[i], i);

            // UI.
            UiFactory.BuildEventSystem();
            var ui = new UiFactory(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                TextureFactory.SpriteAt(GeneratedAssets.Textures, "UiRoundedRect"),
                TextureFactory.SpriteAt(GeneratedAssets.Textures, "UiCircle"),
                TextureFactory.SpriteAt(GeneratedAssets.Textures, "UiStar"), audio);
            HUDController hud = ui.BuildHud(round, portraits, toyIcons.ToArray(), diaperIcon);
            Canvas menuCanvas = ui.Canvas("Menus", 20);
            var menuRoot = (RectTransform)menuCanvas.transform;
            TitleMenu title = ui.BuildTitle(menuRoot);
            DifficultyMenu difficulty = ui.BuildDifficulty(menuRoot);
            InfoMenu howTo = ui.BuildHowToPlay(menuRoot);
            OptionsMenu options = ui.BuildOptions(menuRoot);
            PauseMenu pause = ui.BuildPause(menuRoot);
            ResultsMenu results = ui.BuildResults(menuRoot);

            Canvas loadingCanvas = ui.Canvas("Loading", 100);
            GameObject loadingScreen = ui.BuildLoading((RectTransform)loadingCanvas.transform, out UnityEngine.UI.Image loadingFill);
            var warmup = game.AddComponent<StartupWarmup>();
            warmup.Configure(loadingScreen, loadingFill, arenaCamera.GetComponent<Camera>(), round);

            var flow = game.AddComponent<GameFlow>();
            flow.Configure(round, audio, time, hud, inputs, brains, title, difficulty, howTo, options, pause, results, warmup);
            game.AddComponent<CommandLineSmokeTest>().Configure(flow, round);
        }

        static Sprite BakePortrait(BabyController baby, int index)
        {
            // Apply the runtime looks by hand (they're normally set by BabyAnimator.Start).
            var skin = baby.GetComponentInChildren<SkinnedMeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_AccentColor", ShirtColors[index]);
            block.SetColor("_BaseColor", SkinTones[index]);
            skin.SetPropertyBlock(block);
            var hairBlock = new MaterialPropertyBlock();
            hairBlock.SetColor("_BaseColor", HairColors[index]);
            var tinted = new List<Renderer>();
            foreach (MeshRenderer part in baby.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (part.name.StartsWith("Hair") || part.name == "BrowMesh")
                {
                    part.SetPropertyBlock(hairBlock);
                    tinted.Add(part);
                }
            }

            // Face the sun (it shines toward +Z) and photograph from the lit side.
            Quaternion rotation = baby.transform.rotation;
            baby.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Sprite sprite = IconBaker.Bake($"IconBaby{index + 1}", baby.gameObject, new Vector3(0f, 1.02f, 0f), new Vector3(-0.15f, 0.1f, -1f), 1.6f, 30f);
            baby.transform.rotation = rotation;

            skin.SetPropertyBlock(null);
            foreach (Renderer part in tinted)
                part.SetPropertyBlock(null);
            return sprite;
        }

        static ParticleSystem BurstEmitter(string name, Transform parent, Texture2D texture, ArtKit kit, Color colorA, Color colorB,
            float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = fade;
            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            Material material = kit.Factory.Particle($"Particle{name}", texture);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return particles;
        }

        static void BuildArenaWalls(Transform arena)
        {
            float hx = ArenaHalfExtents.x, hz = ArenaHalfExtents.y, t = WallThickness;
            Wall("Wall_North", arena, new Vector3(0f, 0f, hz + t * 0.5f), new Vector2(hx * 2f + t * 2f, t));
            Wall("Wall_South", arena, new Vector3(0f, 0f, -hz - t * 0.5f), new Vector2(hx * 2f + t * 2f, t));
            Wall("Wall_East", arena, new Vector3(hx + t * 0.5f, 0f, 0f), new Vector2(t, hz * 2f));
            Wall("Wall_West", arena, new Vector3(-hx - t * 0.5f, 0f, 0f), new Vector2(t, hz * 2f));
        }

        /// <summary>Invisible, tall blocker along the playpen fence so nothing flies out.</summary>
        static void Wall(string name, Transform arena, Vector3 position, Vector2 footprint)
        {
            Transform wall = GeneratedAssets.Child(name, arena, position);
            var blocker = wall.gameObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(footprint.x, WallColliderHeight, footprint.y);
            blocker.center = new Vector3(0f, WallColliderHeight * 0.5f, 0f);
        }

        static Transform[] BuildSpawnPoints(Transform arena)
        {
            var spawns = new Transform[SpawnPositions.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                spawns[i] = GeneratedAssets.Child($"Spawn_{i + 1}", arena, SpawnPositions[i]);
                var towardCenter = new Vector3(-SpawnPositions[i].x, 0f, -SpawnPositions[i].z);
                spawns[i].rotation = Quaternion.LookRotation(towardCenter.normalized, Vector3.up);
            }
            return spawns;
        }

        static void RegisterSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Baby Clash";
            PlayerSettings.companyName = "Baby Clash";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.babyclash.game");
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedAssets.Icons}/IconBaby1.png");
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
        }
    }
}
