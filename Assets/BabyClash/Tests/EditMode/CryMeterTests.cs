using System;
using BabyClash.Core;
using NUnit.Framework;

namespace BabyClash.Tests
{
    public sealed class CryMeterTests
    {
        [Test]
        public void StartsEmpty()
        {
            var meter = new CryMeter(100f);
            Assert.AreEqual(0f, meter.Value);
            Assert.IsFalse(meter.IsFull);
        }

        [Test]
        public void CannotExceedMax()
        {
            var meter = new CryMeter(100f);
            meter.Add(90f);
            float applied = meter.Add(50f);

            Assert.AreEqual(100f, meter.Value);
            Assert.AreEqual(10f, applied, 0.0001f);
        }

        [Test]
        public void IgnoresNegativeAndNaNAmounts()
        {
            var meter = new CryMeter(100f);
            meter.Add(20f);
            meter.Add(-15f);
            meter.Add(float.NaN);
            Assert.AreEqual(20f, meter.Value);
        }

        [Test]
        public void ReachingMaxFiresFilledExactlyOnce()
        {
            var meter = new CryMeter(100f);
            int filled = 0;
            meter.Filled += () => filled++;

            for (int i = 0; i < 25; i++)
                meter.Add(5f);

            Assert.IsTrue(meter.IsFull);
            Assert.AreEqual(1, filled);
        }

        [Test]
        public void ManySmallHitsLandExactlyOnFull()
        {
            var meter = new CryMeter(100f);
            for (int i = 0; i < 1000; i++)
                meter.Add(0.1f);
            Assert.IsTrue(meter.IsFull);
            Assert.AreEqual(100f, meter.Value);
        }

        [Test]
        public void ResetEmptiesAndAllowsFillingAgain()
        {
            var meter = new CryMeter(100f);
            int filled = 0;
            meter.Filled += () => filled++;
            meter.Add(100f);

            meter.Reset();
            Assert.AreEqual(0f, meter.Value);
            Assert.IsFalse(meter.IsFull);

            meter.Add(100f);
            Assert.AreEqual(2, filled);
        }

        [Test]
        public void RejectsNonPositiveMax()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CryMeter(0f));
        }
    }
}
