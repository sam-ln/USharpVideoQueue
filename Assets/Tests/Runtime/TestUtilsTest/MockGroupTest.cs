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
            Assert.True(queue1._IsOwner());
            Assert.False(queue2._IsOwner());
        }

        [Test]
        public void TransferOwnership()
        {
           queue2._BecomeOwner();
           Assert.True(queue2._IsOwner());
           Assert.False(queue1._IsOwner());
        }
        
        
        [Test]
        public void GetLocalPlayerId()
        {
            foreach (var mockSet in MockGroup.MockSets)
            {
                VideoQueue queue = mockSet.VideoQueueMock.Object;
                Assert.AreEqual(queue._GetPlayerID(queue._GetLocalPlayer()),mockSet.PlayerId);
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
                    Assert.AreEqual(queue._GetPlayerID(remoteMockSet.Player), remoteMockSet.PlayerId);
                }
            }
           
        }

        [Test]
        public void RemovePlayer()
        {
            int removedPlayer = MockGroup.MockSets[0].PlayerId;
            Assert.AreEqual(true,queue2._IsPlayerWithIDValid(removedPlayer));
            MockGroup.SimulatePlayerLeft(removedPlayer);
            Assert.AreEqual(false,queue2._IsPlayerWithIDValid(removedPlayer));
        }
        
        
    }
}