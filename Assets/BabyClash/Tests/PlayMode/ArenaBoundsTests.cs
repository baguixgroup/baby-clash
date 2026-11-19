using System.Collections;
using BabyClash.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BabyClash.Tests
{
    public sealed class ArenaBoundsTests
    {
        TestArena arena;

        [SetUp]
        public void SetUp() => arena = new TestArena();

        [TearDown]
        public void TearDown() => arena.Dispose();

        [UnityTest]
        public IEnumerator EscapedDiaperIsReturned_NotReplaced()
        {
            Diaper diaper = arena.CreateDiaper("Diaper_01", new Vector3(20f, 1f, 0f));
            ArenaBounds bounds = arena.CreateBounds(new Vector2(5f, 5f), new BabyController[0], new Throwable[] { diaper });
            int recovered = 0;
            diaper.Recovered += _ => recovered++;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, recovered);
            Assert.IsTrue(bounds.IsInside(diaper.transform.position));
            Assert.AreEqual(ItemState.Loose, diaper.State);
        }

        [UnityTest]
        public IEnumerator EscapedBabyIsPutBackInside()
        {
            BabyController baby = arena.CreateBaby(0, new Vector3(20f, 0f, 0f), Vector3.right);
            ArenaBounds bounds = arena.CreateBounds(new Vector2(5f, 5f), new[] { baby }, new Throwable[0]);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(bounds.IsInside(baby.transform.position));
        }
    }
}
