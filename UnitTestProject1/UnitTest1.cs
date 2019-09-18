using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    public class Dur
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsIncluded(DateTime dt, int tolerance)
        {
            if (dt >= StartDate && dt <= EndDate) return true;

            var earlierTol = dt.AddMinutes(tolerance * -1);
            if (earlierTol >= StartDate && earlierTol <= EndDate) return true;
            if (earlierTol <= StartDate && EndDate <= dt) return true;
            var laterTol = dt.AddMinutes(tolerance);
            if (laterTol <= StartDate && EndDate <= dt) return true;

            return false;
        }
    }

    [TestClass]
    public class UnitTest1
    {
        Dur d1 = new Dur { StartDate = Convert.ToDateTime("9/9/2018 12:00 PM"), EndDate = Convert.ToDateTime("9/9/2018 12:10 PM") };
        Dur d2 = new Dur { StartDate = Convert.ToDateTime("9/9/2018 12:10 PM"), EndDate = Convert.ToDateTime("9/9/2018 12:20 PM") };
        Dur d3 = new Dur { StartDate = Convert.ToDateTime("9/9/2018 12:20 PM"), EndDate = Convert.ToDateTime("9/9/2018 12:30 PM") };

        DateTime td1 = Convert.ToDateTime("9/9/2018 12:13 PM");

        [TestMethod]
        public void TestMethod1()
        {
            Assert.IsTrue(d1.IsIncluded(td1, 5));
        }
        [TestMethod]
        public void TestMethod2()
        {
            Assert.IsTrue(d2.IsIncluded(td1, 5));
        }
        [TestMethod]
        public void TestMethod3()
        {
            Assert.IsFalse(d3.IsIncluded(td1, 5));
        }
        [TestMethod]
        public void TestMethod4()
        {
            Assert.IsTrue(d1.IsIncluded(td1, 20));
        }
        [TestMethod]
        public void TestMethod5()
        {
            Assert.IsTrue(d2.IsIncluded(td1, 20));
        }
        [TestMethod]
        public void TestMethod6()
        {
            Assert.IsTrue(d3.IsIncluded(td1, 20));
        }
    }
}
