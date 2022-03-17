// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using GgpGrpc.Cloud;
using GgpGrpc.Cloud.Interceptors;
using Grpc.Core;
using Microsoft.VisualStudio.Threading;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using YetiCommon;
using YetiVSITestsCommon;
using System.Threading.Tasks;
using Metrics.Shared;
using YetiVSI.Metrics;
using ConfigurationException = YetiCommon.ConfigurationException;

namespace YetiVSI.Test.Metrics
{
    [TestFixture]
    public class ActionRecorderTests
    {
        class TestException : Exception
        {
            public TestException() : base() { }
            public TestException(string msg) : base(msg) { }
            public TestException(string msg, Exception inner) : base(msg, inner) { }
        }

        const ActionType MetricActionType = ActionType.GameletsList;
        const DeveloperEventType.Types.Type EventType =
            DeveloperEventType.Types.Type.VsiGameletsList;
        const long ElapsedMilliseconds = 1234;
        const string DebugSessionId = "abc123";
        const string FakeServiceName = "foo";
        const string FakeMethodName = "bar";

        readonly IMethod FakeMethod = new Method<string, string>(MethodType.Unary,
                FakeServiceName, FakeMethodName, Marshallers.StringMarshaller,
                Marshallers.StringMarshaller);

        readonly DeveloperLogEvent.Types.GameletData FakeGameletData =
            new DeveloperLogEvent.Types.GameletData
            {
                Type = DeveloperLogEvent.Types.GameletData.Types.Type.GameletEdge
            };
        // Object under test
        ActionRecorder actionRecorder;

        // Substitutions
        IVsiMetrics metrics;
        ITimer timer;
        Timer.Factory timerFactory;

        // Prototype of expected event proto
        DeveloperLogEvent logEvent;

        [SetUp]
        public void SetUp()
        {
            metrics = Substitute.For<IVsiMetrics>();
            timer = Substitute.For<ITimer>();
            timer.ElapsedMilliseconds.Returns(ElapsedMilliseconds);
            timerFactory = Substitute.For<Timer.Factory>();
            timerFactory.Create().Returns(timer);

            actionRecorder = new ActionRecorder(metrics, timerFactory);

            logEvent = new DeveloperLogEvent
            {
                StatusCode = DeveloperEventStatus.Types.Code.Success,
                LatencyMilliseconds = ElapsedMilliseconds,
                LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool
            };
        }

        [Test]
        public void TestCreateStartedTimer()
        {
            var timer = actionRecorder.CreateStartedTimer();
            Assert.AreEqual(timer.ElapsedMilliseconds, ElapsedMilliseconds);
        }

        [Test]
        public void Record()
        {
            bool ranTask = false;
            actionRecorder.RecordToolAction(MetricActionType, delegate { ranTask = true;  });

            Assert.IsTrue(ranTask);
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [TestCase(typeof(InputException), DeveloperEventStatus.Types.Code.InvalidInput)]
        [TestCase(typeof(ConfigurationException),
            DeveloperEventStatus.Types.Code.InvalidConfiguration)]
        [TestCase(typeof(InvalidStateException),
            DeveloperEventStatus.Types.Code.InvalidObjectState)]
        [TestCase(typeof(TestException), DeveloperEventStatus.Types.Code.InternalError)]
        [TestCase(typeof(OperationCanceledException),
            DeveloperEventStatus.Types.Code.CanceledSubTask)]
        [TestCase(typeof(TimeoutException), DeveloperEventStatus.Types.Code.Timeout)]
        [TestCase(typeof(ProcessException),
            DeveloperEventStatus.Types.Code.ExternalToolUnavailable)]
        public void Record_Error(Type exceptionType, DeveloperEventStatus.Types.Code statusCode)
        {
            Assert.Throws(exceptionType,
                delegate
                {
                    actionRecorder.RecordToolAction(MetricActionType,
                        () => {
                            throw (Exception)Activator.CreateInstance(exceptionType, "Test");
                        });
                });

            logEvent.StatusCode = statusCode;
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [Test]
        public void Record_InnerError()
        {
            Assert.Throws<TestException>(
                delegate
                {
                    actionRecorder.RecordToolAction(MetricActionType,
                        delegate {
                            throw new TestException("Test", new ConfigurationException("inner"));
                        });
                });

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.InvalidConfiguration;
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [TestCase(StatusCode.Internal, DeveloperEventStatus.Types.Code.CommandFailure)]
        [TestCase(StatusCode.Unavailable, DeveloperEventStatus.Types.Code.ConnectionFailure)]
        [TestCase(StatusCode.Unauthenticated,
            DeveloperEventStatus.Types.Code.AuthorizationFailure)]
        [TestCase(StatusCode.Unknown, DeveloperEventStatus.Types.Code.InternalError)]
        public void Record_RpcError(StatusCode rpcCode, DeveloperEventStatus.Types.Code statusCode)
        {
            Assert.Throws<CloudException>(
                delegate
                {
                    actionRecorder.RecordToolAction(MetricActionType,
                        delegate {
                            try
                            {
                                throw new RpcException(new Status(rpcCode, "test"));
                            }
                            catch (RpcException e)
                            {
                                if (rpcCode != StatusCode.Unknown)
                                {
                                    e.Data[ErrorData.ServiceNameKey] = FakeServiceName;
                                    e.Data[ErrorData.MethodNameKey] = FakeMethodName;
                                }
                                throw new CloudException("Test", e);
                            }
                        });
                });

            var expectedDetails = new GrpcServiceCallDetails
            {
                Status = new Status(rpcCode, null)
            };
            if (rpcCode != StatusCode.Unknown)
            {
                expectedDetails.ServiceName = FakeServiceName;
                expectedDetails.ServiceMethod = FakeMethodName;
            }

            logEvent.StatusCode = statusCode;
            logEvent.GrpcErrorDetails = expectedDetails;
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [Test]
        public void Record_ProcessExecutionError()
        {
            Assert.Throws<DeployException>(
                delegate
                {
                    actionRecorder.RecordToolAction(MetricActionType,
                        delegate {
                            try
                            {
                                throw new ProcessExecutionException("test", 1);
                            }
                            catch (ProcessExecutionException e)
                            {
                                throw new DeployException("test wrapped", e);
                            }
                        });
                });
            var externalToolError =
                new DeveloperLogEvent.Types.ExternalToolError {ExitCode = 1};
            logEvent.StatusCode = DeveloperEventStatus.Types.Code.ExternalToolFailure;
            logEvent.ExternalToolError = externalToolError;
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RecordCancelable(bool canceled)
        {
            var expectedStatus = !canceled
                ? DeveloperEventStatus.Types.Code.Success
                : DeveloperEventStatus.Types.Code.Cancelled;
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var task = FakeCancelableTask.CreateFactory(new JoinableTaskContext(), canceled)
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
                .Create("test", _ => { });

            Assert.AreEqual(!canceled, task.RunAndRecord(actionRecorder, MetricActionType));

            logEvent.StatusCode = expectedStatus;
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [Test]
        public void RecordCancelable_Error()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var task = FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false)
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
                .Create("test", _ => { throw new TestException("Test"); });
            Assert.Throws<TestException>(() => task.RunAndRecord(actionRecorder, MetricActionType));

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.InternalError;
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [Test]
        public void RecordCancelable_CancelledSubTask()
        {
            var metricActionInnerType = ActionType.GameletSelect;
            var innerEventType = DeveloperEventType.Types.Type.VsiGameletsSelect;
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var task = FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false)
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
                .Create("test", _ =>
                {
                    actionRecorder.RecordToolAction(metricActionInnerType,
                        () => { throw new OperationCanceledException(); });
                });
            Assert.IsFalse(task.RunAndRecord(actionRecorder, MetricActionType));

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.Cancelled;
            metrics.Received(1).RecordEvent(EventType, logEvent);

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.CanceledSubTask;
            metrics.Received(1).RecordEvent(innerEventType, logEvent);

            Received.InOrder(() =>
            {
                timer.Start();
                timer.Start();
                timer.Stop();
                var unused = timer.ElapsedMilliseconds;
                timer.Stop();
                unused = timer.ElapsedMilliseconds;
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RecordCancelableModal(bool completed)
        {
            bool ranTask = false;
            actionRecorder.RecordUserAction(MetricActionType,
                delegate ()
                {
                    ranTask = true;
                    return completed;
                });

            var expectedStatus = completed
                ? DeveloperEventStatus.Types.Code.Success
                : DeveloperEventStatus.Types.Code.Cancelled;
            Assert.IsTrue(ranTask);

            logEvent.StatusCode = expectedStatus;
            logEvent.LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyUser;
            metrics.Received(1).RecordEvent(EventType, logEvent);
        }

        [Test]
        public void RecordCancelableModal_Error()
        {
            Assert.Throws<TestException>(
                delegate {
                    actionRecorder.RecordUserAction(MetricActionType,
                        () => { throw new TestException("Test"); });
                    });

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.InternalError;
            logEvent.LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyUser;
            metrics.Received(1).RecordEvent(EventType, logEvent);
        }

        [Test]
        public void RecordThroughExtension()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            var task = FakeCancelableTask.CreateFactory(new JoinableTaskContext(), false)
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
                .Create("test", _ => { });
            task.RunAndRecord(actionRecorder, MetricActionType);

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.Success;
            metrics.Received(1).RecordEvent(EventType, logEvent);
        }

        [Test]
        public void RecordExternalDuration()
        {
            actionRecorder.RecordToolAction(MetricActionType, timer);

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.Success;
            logEvent.LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool;
            logEvent.LatencyMilliseconds = ElapsedMilliseconds;
            metrics.Received(1).RecordEvent(EventType, logEvent);
        }

        [Test]
        public void RecordExternalDurationWithDetails()
        {
            var bbData = new VSIBoundBreakpointsData {NumPendingBreakpoints = 1};
            actionRecorder.RecordToolAction(MetricActionType, timer,
                new DeveloperLogEvent {BoundBreakpointsData = bbData});

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.Success;
            logEvent.LatencyType = DeveloperLogEvent.Types.LatencyType.LatencyTool;
            logEvent.LatencyMilliseconds = ElapsedMilliseconds;
            logEvent.BoundBreakpointsData = bbData;
            metrics.Received(1).RecordEvent(EventType, logEvent);
        }

        [Test]
        public void RecordSuccess()
        {
            actionRecorder.RecordSuccess(MetricActionType);

            metrics.Received(1).RecordEvent(EventType,
                new DeveloperLogEvent {StatusCode = DeveloperEventStatus.Types.Code.Success});
        }

        [Test]
        public void RecordSuccessWithDetails()
        {
            actionRecorder.RecordSuccess(MetricActionType,
                new DeveloperLogEvent {GameletData = FakeGameletData});

            metrics.Received(1).RecordEvent(EventType,
                new DeveloperLogEvent
                {
                    StatusCode = DeveloperEventStatus.Types.Code.Success,
                    GameletData = FakeGameletData
                });
        }

        [Test]
        public void RecordFailure()
        {
            actionRecorder.RecordFailure(MetricActionType, new TestException("Test"),
                new DeveloperLogEvent {GameletData = FakeGameletData});

            metrics.Received(1).RecordEvent(EventType,
                new DeveloperLogEvent
                {
                    StatusCode = DeveloperEventStatus.Types.Code.InternalError,
                    GameletData = FakeGameletData
                });
        }

        [Test]
        public void StartActionUpdateAndRecord()
        {
            var pendingAction = actionRecorder.CreateToolAction(MetricActionType);
            timer.DidNotReceive().Start();

            pendingAction.UpdateEvent(new DeveloperLogEvent {GameletData = FakeGameletData});
            pendingAction.Record(() => { });

            logEvent.GameletData = FakeGameletData;
            metrics.Received(1).RecordEvent(EventType, logEvent);

            AssertTimerUsedCorrectly();
        }

        [Test]
        public async Task RecordAsyncSuccessAsync()
        {
            var gameletData = new DeveloperLogEvent.Types.GameletData
            {
                Type = DeveloperLogEvent.Types.GameletData.Types.Type.GameletEdge
            };
            var action = actionRecorder.CreateToolAction(MetricActionType);
            await action.RecordAsync(Task.Run(() =>
            {
                action.UpdateEvent(new DeveloperLogEvent {GameletData = gameletData});
                action.Record(FakeMethod, Status.DefaultSuccess, 1000);
            }));

            var expectedRpcDetails = new GrpcServiceCallDetails
            {
                ServiceName = FakeServiceName,
                ServiceMethod = FakeMethodName,
                Status = new Status(StatusCode.OK, null),
                RoundtripLatency = 1000 * 1000  // 1000ms in us
            };
            
            logEvent.GrpcCallDetails.Add(expectedRpcDetails);
            logEvent.GameletData = gameletData;
            metrics.Received(1).RecordEvent(EventType, logEvent);

            AssertTimerUsedCorrectly();
        }

        [Test]
        public void RecordAsyncException()
        {
            var rpcAction = actionRecorder.CreateToolAction(MetricActionType);
            Task task = Task.Run(delegate { throw new Exception(); });

            Assert.ThrowsAsync<Exception>(
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                () => rpcAction.RecordAsync(task));
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

            logEvent.StatusCode = DeveloperEventStatus.Types.Code.InternalError;
            metrics.Received(1).RecordEvent(EventType, logEvent);

            AssertTimerUsedCorrectly();
        }

        [Test]
        public void RecordRpcStats()
        {
            var gameletData = new DeveloperLogEvent.Types.GameletData
            {
                Type = DeveloperLogEvent.Types.GameletData.Types.Type.GameletEdge
            };
            var rpcAction = actionRecorder.CreateToolAction(MetricActionType);
            rpcAction.Record(delegate {
                rpcAction.UpdateEvent(new DeveloperLogEvent { GameletData = gameletData });
                (rpcAction as RpcRecorder).Record(FakeMethod, Status.DefaultSuccess, 1000);
            });

            var expectedRpcDetails = new GrpcServiceCallDetails
            {
                ServiceName = FakeServiceName,
                ServiceMethod = FakeMethodName,
                Status = new Status(StatusCode.OK, null),
                RoundtripLatency = 1000 * 1000  // 1000ms in us
            };

            if (logEvent.GrpcCallDetails == null)
            {
                logEvent.GrpcCallDetails = new List<GrpcServiceCallDetails>();
            }

            logEvent.GrpcCallDetails.Add(expectedRpcDetails);
            logEvent.GameletData = gameletData;
            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [Test]
        public void RecordOnInvalidAction()
        {
            var rpcAction = actionRecorder.CreateToolAction(MetricActionType);
            rpcAction.Record(delegate { });

            Assert.Throws(typeof(InvalidOperationException),
                delegate
                {
                    rpcAction.Record(delegate { });
                });

            Assert.Throws(typeof(InvalidOperationException),
                delegate
                {
                    (rpcAction as RpcRecorder).Record(FakeMethod, Status.DefaultSuccess, 1000);
                });


            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        [Test]
        public void UpdateActionOnInvalidAction()
        {
            var rpcAction = actionRecorder.CreateToolAction(MetricActionType);
            rpcAction.Record(delegate { });

            Assert.Throws(typeof(InvalidOperationException),
                delegate
                {
                    rpcAction.UpdateEvent(new DeveloperLogEvent
                    {
                        GameletData = new DeveloperLogEvent.Types.GameletData()
                        {
                            Type = DeveloperLogEvent.Types.GameletData.Types.Type.GameletEdge
                        }
                    });
                });

            metrics.Received(1).RecordEvent(EventType, logEvent);
            AssertTimerUsedCorrectly();
        }

        // Asserts that the timer was used correctly: start, stop, then read elapsed time.
        void AssertTimerUsedCorrectly()
        {
            Received.InOrder(() =>
            {
                timer.Start();
                timer.Stop();
                var unused = timer.ElapsedMilliseconds;
            });
        }
    }
}
