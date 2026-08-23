using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Moq;
using NUnit.Framework;

namespace USharpVideoQueue.Tests.Runtime.TestUtils
{
    /// <summary>
    /// A mock for classes whose methods are not virtual, used like a Moq mock.
    /// </summary>
    /// <remarks>
    /// Moq can only replace virtual methods. USharpVideoPlayer has none, and it ships as its own
    /// package, so we cannot add them. <see cref="HarmonyPatches"/> catches its calls instead and
    /// reports them here, which keeps Setup and Verify reading the same as they would with Moq.
    /// </remarks>
    public sealed class NonVirtualMock<T> : ICallRecorder where T : class
    {
        private readonly List<Call> calls = new List<Call>();
        private readonly List<CallSetup> setups = new List<CallSetup>();

        public NonVirtualMock()
        {
            HarmonyPatches.Install();
            // Moq only builds the instance here. It cannot intercept anything on it.
            Object = new Mock<T>().Object;
            MockRegistry.Add(Object, this);
        }

        /// <summary>The instance to give to the code under test.</summary>
        public T Object { get; }

        /// <summary>Runs a callback whenever a matching call happens.</summary>
        public CallbackSetup Setup(Expression<Action<T>> call)
        {
            CallSetup setup = new CallSetup(CallPattern.From(call));
            setups.Add(setup);
            return new CallbackSetup(setup);
        }

        /// <summary>Fails the test unless a matching call happened the expected number of times.</summary>
        public void Verify(Expression<Action<T>> call, Times times)
        {
            CallPattern expected = CallPattern.From(call);
            int actual = calls.Count(expected.Matches);
            if (times.Validate(actual)) return;

            string happened = calls.Count == 0
                ? "  (no calls)"
                : string.Join(Environment.NewLine, calls.Select(made => $"  {made}"));

            throw new AssertionException(
                $"Expected {expected} {Describe(times)}, but it happened {actual} time(s)." +
                $"{Environment.NewLine}Calls on {typeof(T).Name}:{Environment.NewLine}{happened}");
        }

        /// <summary>Accepts <c>Times.Once</c> written without brackets, the way Moq does.</summary>
        public void Verify(Expression<Action<T>> call, Func<Times> times) => Verify(call, times());

        void ICallRecorder.Record(Call call)
        {
            calls.Add(call);

            // Copied first, because a callback may add setups of its own.
            foreach (CallSetup setup in setups.ToArray())
            {
                if (setup.Pattern.Matches(call)) setup.Callback?.Invoke();
            }
        }

        private static string Describe(Times times)
        {
            times.Deconstruct(out int from, out int to);
            if (from == to) return $"exactly {from} time(s)";
            if (to == int.MaxValue) return $"at least {from} time(s)";
            if (from == 0) return $"at most {to} time(s)";
            return $"between {from} and {to} times";
        }
    }

    /// <summary>Lets <c>Setup(...).Callback(...)</c> read the same as it does with Moq.</summary>
    public sealed class CallbackSetup
    {
        private readonly CallSetup setup;

        internal CallbackSetup(CallSetup setup) => this.setup = setup;

        public void Callback(Action callback) => setup.Callback = callback;
    }

    /// <summary>A call that happened: the method name and the arguments it got.</summary>
    internal sealed class Call
    {
        public Call(string method, object[] arguments)
        {
            Method = method;
            Arguments = arguments ?? new object[0];
        }

        public string Method { get; }
        public object[] Arguments { get; }

        public override string ToString() =>
            $"{Method}({string.Join(", ", Arguments.Select(argument => argument?.ToString() ?? "null"))})";
    }

    /// <summary>
    /// The calls a lambda such as <c>player => player.PlayVideo(url)</c> describes. An
    /// <c>It.IsAny&lt;T&gt;()</c> argument matches any value.
    /// </summary>
    internal sealed class CallPattern
    {
        private static readonly object AnyArgument = new object();

        private readonly string method;
        private readonly object[] arguments;

        private CallPattern(string method, object[] arguments)
        {
            this.method = method;
            this.arguments = arguments;
        }

        public static CallPattern From(LambdaExpression lambda)
        {
            if (!(lambda.Body is MethodCallExpression call))
            {
                throw new ArgumentException($"Expected a method call, but got: {lambda.Body}", nameof(lambda));
            }

            return new CallPattern(call.Method.Name, call.Arguments.Select(ExpectedValue).ToArray());
        }

        public bool Matches(Call call)
        {
            if (call.Method != method) return false;
            if (call.Arguments.Length != arguments.Length) return false;

            for (int i = 0; i < arguments.Length; i++)
            {
                if (!Matches(arguments[i], call.Arguments[i])) return false;
            }

            return true;
        }

        public override string ToString()
        {
            IEnumerable<string> shown = arguments.Select(argument =>
                ReferenceEquals(argument, AnyArgument) ? "any" : argument?.ToString() ?? "null");
            return $"{method}({string.Join(", ", shown)})";
        }

        private static bool Matches(object expected, object actual)
        {
            if (ReferenceEquals(expected, AnyArgument)) return true;
            if (ReferenceEquals(expected, actual)) return true;

            // Unity considers every object without a GameObject equal to every other one, so two
            // different mocks would compare as the same. Those may only be matched by reference.
            if (expected is UnityEngine.Object || actual is UnityEngine.Object) return false;

            return Equals(expected, actual);
        }

        private static object ExpectedValue(Expression argument)
        {
            if (argument is MethodCallExpression call
                && call.Method.DeclaringType == typeof(It)
                && call.Method.Name == "IsAny")
            {
                return AnyArgument;
            }

            if (argument is ConstantExpression constant) return constant.Value;

            return Expression.Lambda<Func<object>>(Expression.Convert(argument, typeof(object))).Compile()();
        }
    }

    internal sealed class CallSetup
    {
        public CallSetup(CallPattern pattern) => Pattern = pattern;

        public CallPattern Pattern { get; }
        public Action Callback { get; set; }
    }

    internal interface ICallRecorder
    {
        void Record(Call call);
    }

    /// <summary>Where <see cref="HarmonyPatches"/> looks up the mock belonging to an instance.</summary>
    internal static class MockRegistry
    {
        private static readonly Dictionary<object, ICallRecorder> Mocks =
            new Dictionary<object, ICallRecorder>(ReferenceComparer.Instance);

        public static void Add(object instance, ICallRecorder mock) => Mocks[instance] = mock;

        /// <summary>Records a call if the instance is mocked.</summary>
        /// <returns>False if it is not, which means the real method should run.</returns>
        public static bool TryRecord(object instance, string method, params object[] arguments)
        {
            if (instance == null || !Mocks.TryGetValue(instance, out ICallRecorder mock)) return false;

            mock.Record(new Call(method, arguments));
            return true;
        }
    }

    /// <summary>
    /// Compares by reference. Needed because Unity objects without a GameObject all look equal.
    /// </summary>
    internal sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
