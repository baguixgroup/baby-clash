using System;
using UnityEngine;

namespace BabyClash
{
    /// <summary>Grows the scene's two babies to four (with their diapers, spawns and colours) when the game loads.</summary>
    public static class BabyRoster
    {
        static readonly Color[] shirts = { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.5f, 0.35f), new Color(0.4f, 0.82f, 0.45f), new Color(0.8f, 0.5f, 1f) };
        static readonly Color[] skins = { Color.white, new Color(0.88f, 0.72f, 0.62f), new Color(0.78f, 0.6f, 0.48f), new Color(1f, 0.95f, 0.88f) };
        static readonly Color[] hairs = { new Color(0.45f, 0.28f, 0.15f), new Color(0.16f, 0.1f, 0.08f), new Color(0.12f, 0.09f, 0.1f), new Color(0.95f, 0.55f, 0.2f) };
        static readonly Vector3[] spawns = { new Vector3(-5.5f, 0f, 0f), new Vector3(5.5f, 0f, 0f), new Vector3(-0.8f, 0f, 3.9f), new Vector3(0.8f, 0f, -3.9f) };

        public static BabyController[] Expand(RoundManager round, ArenaBounds arena, ArenaCamera view)
        {
            if (round.AllBabies.Count >= MatchSetup.MaxPlayers || round.AllBabies.Count < 2)
            {
                var same = new BabyController[round.AllBabies.Count];
                for (int i = 0; i < same.Length; i++)
                    same[i] = round.AllBabies[i];
                return same;
            }

            int have = round.AllBabies.Count;
            var babies = new BabyController[MatchSetup.MaxPlayers];
            var diapers = new Diaper[babies.Length];
            var points = new Transform[babies.Length];
            for (int i = 0; i < babies.Length; i++)
            {
                points[i] = new GameObject($"RuntimeSpawn_{i + 1}").transform;
                points[i].SetPositionAndRotation(spawns[i], Quaternion.LookRotation(-spawns[i].normalized, Vector3.up));
                if (i < have)
                {
                    babies[i] = round.AllBabies[i];
                    diapers[i] = round.Diapers[i];
                    continue;
                }
                BabyController source = round.AllBabies[i % have];
                // Built while inactive, so it wakes up with its own identity.
                bool wasActive = source.gameObject.activeSelf;
                source.gameObject.SetActive(false);
                babies[i] = UnityEngine.Object.Instantiate(source, spawns[i], points[i].rotation);
                source.gameObject.SetActive(wasActive);
                babies[i].name = $"Baby_{i + 1}";
                babies[i].Configure(source.Config, i, $"BABY {i + 1}", shirts[i]);
                babies[i].GetComponentInChildren<BabyAnimator>(true).ConfigureLooks(skins[i], hairs[i]);
                babies[i].GetComponent<BabyInputHandler>().enabled = false;
                babies[i].GetComponent<BabyBrain>().enabled = false;
                babies[i].gameObject.SetActive(true);
                diapers[i] = UnityEngine.Object.Instantiate(round.Diapers[i % have], round.Diapers[0].transform.parent);
                diapers[i].name = $"Diaper_{i + 1:00}";
            }
            round.SetRoster(babies, points, diapers);
            if (arena != null)
                arena.Track(babies, new Throwable[] { diapers[2], diapers[3] });
            if (view != null)
                view.SetTargets(Array.ConvertAll(babies, baby => baby.transform));
            return babies;
        }
    }
}
