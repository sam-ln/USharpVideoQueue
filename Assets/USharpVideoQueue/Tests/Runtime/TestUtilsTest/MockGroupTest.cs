using NUnit.Framework;
using USharpVideoQueue.Runtime;
using USharpVideoQueue.Tests.Runtime.TestUtils;

namespace USharpVideoQueue.Tests.Runtime.TestUtilsTest
{
    public class MockGroupTest
    {
        private UdonSharpTestUtils.VideoQueueMockGroup MockGroup;
        private VideoQueue queue1;
        private VideoQueue queue2;
        
        [SetUp]
        public void Prepare()
        {
            MockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            queue1 = MockGroup.MockSets[0].VideoQueueMock.Object;
            queue2 = MockGroup.MockSets[1].VideoQueueMock.Object;
        }

        [Test]
        public void CorrectDefaultOwnership()
        {
            Assert.True(queue1.isOwner());
            Assert.False(queue2.isOwner());
        }

        [Test]
        public void TransferOwnership()
        {
           queue2.becomeOwner();
           Assert.True(queue2.isOwner());
           Assert.False(queue1.isOwner());
        }
        
        [Test]
        public void ServerTimeIncreasesOnSerialization()
        {
            int initialTime = MockGroup.ServerTime;
            queue1.synchronizeData();
            Assert.AreEqual(queue1.getCurrentServerTime(), queue2.getCurrentServerTime());
            Assert.True(queue1.getCurrentServerTime() > initialTime);
           
        }
        
        [Test]
        public void GetLocalPlayerId()
        {
            foreach (var mockSet in MockGroup.MockSets)
            {
                VideoQueue queue = mockSet.VideoQueueMock.Object;
                Assert.AreEqual(queue.getPlayerID(queue.getLocalPlayer()),mockSet.PlayerId);
            }
           
        }
        
        [Test]
        public void GetRemotePlayerId()
        {
            foreach (var mockSet in MockGroup.MockSets)
            {
                VideoQueue queue = mockSet.VideoQueueMock.Object;
                foreach (var remoteMockSet in MockGroup.MockSets)
                {
                    Assert.AreEqual(queue.getPlayerID(remoteMockSet.Player), remoteMockSet.PlayerId);
                }
            }
           
        }

        [Test]
        public void RemovePlayer()
        {
            int removedPlayer = MockGroup.MockSets[0].PlayerId;
            Assert.AreEqual(true,queue2.isPlayerWithIDValid(removedPlayer));
            MockGroup.SimulatePlayerLeft(removedPlayer);
            Assert.AreEqual(false,queue2.isPlayerWithIDValid(removedPlayer));
        }
        
        
    }
}