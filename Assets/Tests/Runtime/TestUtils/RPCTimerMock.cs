using System.Collections.Generic;
using Moq;
using UdonSharp;

namespace USharpVideoQueue.Tests.Runtime.TestUtils
{
    /// <summary>
    /// Stands in for the RPCTimer, so scheduled calls happen when the test says so instead of after
    /// real time passes.
    /// </summary>
    /// <remarks>
    /// The real timer counts down in Update using the server clock, and neither of those runs in an
    /// edit mode test. RPCTimer declares its methods virtual for this reason, so Moq can replace
    /// them directly.
    ///
    /// Scheduling works like the real timer: a delay of zero happens straight away, anything longer
    /// waits for <see cref="FireAll"/>, and a call is removed before it runs so that a call which
    /// schedules a new one does not happen twice.
    /// </remarks>
    public sealed class RPCTimerMock
    {
        private readonly List<ScheduledCall> scheduled = new List<ScheduledCall>();

        private readonly Mock<RPCTimer> mock;

        private int nextId = 1;

        public RPCTimerMock()
        {
            mock = new Mock<RPCTimer>();

            mock.Setup(timer => timer.Schedule(
                    It.IsAny<UdonSharpBehaviour>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<object[]>()))
                .Returns((UdonSharpBehaviour target, string method, float delay, object[] arguments) =>
                    Schedule(target, method, delay, arguments));

            mock.Setup(timer => timer.CancelRunningAndSchedule(
                    It.IsAny<UdonSharpBehaviour>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<float>(),
                    It.IsAny<object[]>()))
                .Returns((UdonSharpBehaviour target, int runningId, string method, float delay, object[] arguments) =>
                {
                    if (runningId != -1) Cancel(runningId);
                    return Schedule(target, method, delay, arguments);
                });

            mock.Setup(timer => timer.Cancel(It.IsAny<int>())).Returns((int id) => Cancel(id));

            mock.Setup(timer => timer.CancelAll()).Callback(() => scheduled.Clear());

            mock.Setup(timer => timer.CancelAllFor(It.IsAny<UdonSharpBehaviour>()))
                .Callback((UdonSharpBehaviour target) =>
                    scheduled.RemoveAll(call => ReferenceEquals(call.Target, target)));
        }

        /// <summary>The timer to give to the queue.</summary>
        public RPCTimer Object => mock.Object;

        /// <summary>How many calls are still waiting to happen.</summary>
        public int PendingCount => scheduled.Count;

        public bool HasPending => PendingCount > 0;

        /// <summary>
        /// Makes every waiting call happen now, in the order it was scheduled. Calls scheduled by
        /// those keep waiting until the next time this is called.
        /// </summary>
        public void FireAll()
        {
            ScheduledCall[] due = scheduled.ToArray();
            scheduled.Clear();

            foreach (ScheduledCall call in due)
            {
                UdonSharpTestUtils.SimulateSendCustomEvent(call.Target, call.Method, call.Arguments);
            }
        }

        private int Schedule(UdonSharpBehaviour target, string method, float delay, object[] arguments)
        {
            // ReferenceEquals, not ==: Unity reports objects without a GameObject as null.
            if (ReferenceEquals(target, null) || string.IsNullOrEmpty(method)) return -1;

            int id = nextId++;

            if (delay <= 0f)
            {
                UdonSharpTestUtils.SimulateSendCustomEvent(target, method, arguments);
                return id;
            }

            scheduled.Add(new ScheduledCall(id, target, method, arguments));
            return id;
        }

        private bool Cancel(int id) => scheduled.RemoveAll(call => call.Id == id) > 0;

        private sealed class ScheduledCall
        {
            public ScheduledCall(int id, UdonSharpBehaviour target, string method, object[] arguments)
            {
                Id = id;
                Target = target;
                Method = method;
                Arguments = arguments ?? new object[0];
            }

            public int Id { get; }
            public UdonSharpBehaviour Target { get; }
            public string Method { get; }
            public object[] Arguments { get; }
        }
    }
}
