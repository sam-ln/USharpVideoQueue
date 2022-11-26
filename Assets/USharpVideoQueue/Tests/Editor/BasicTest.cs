
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;

namespace USharpVideoQueue.Tests.Editor
{
    public class BasicTest
    {
        private VideoQueue queue;

        [SetUp]
        public void Prepare() {
            queue = new GameObject().AddComponent<VideoQueue>();
            queue.Start();
        }

        /**
        * 
        */
        [Test]
        public void CreateBehavior()
        {  
            Assert.NotNull(queue);
            Assert.True(queue.Initialized);    
        }


        /*
        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator BasicTestWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }
        */
    }
}
