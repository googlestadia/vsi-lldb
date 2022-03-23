using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Metrics.Shared.Tests
{
    [TestFixture]
    public class ExceptionWriterTests
    {
        const int _maxExceptionsChainLength = 2;
        const int _maxStackTraceFrames = 2;
        ExceptionWriter _target;

        [SetUp]
        public void Setup()
        {
            _target = new ExceptionWriter(new[] { "System", "Metrics" }, _maxExceptionsChainLength,
                                          _maxStackTraceFrames);
        }

        [Test]
        public void Write()
        {
            var ex = new TestException1("outer", new TestException2());

            var actualData = new VSIExceptionData();
            _target.WriteToExceptionData(TestClass.MethodInfo1, ex, actualData);

            var expectedData = new VSIExceptionData
            {
                CatchSite = ExceptionWriter.GetProto(TestClass.MethodInfo1)
            };
            expectedData.ExceptionsChain.Add(new VSIExceptionData.Types.Exception
            {
                ExceptionType = ExceptionWriter.GetProto(typeof(TestException1))
            });
            expectedData.ExceptionsChain.Add(new VSIExceptionData.Types.Exception
            {
                ExceptionType = ExceptionWriter.GetProto(typeof(TestException2))
            });

            Assert.AreEqual(expectedData, actualData);
        }

        [Test]
        public void WriteWithStackTrace()
        {
            var ex = new TestException2();

            // Throw exception to capture the stack trace
            try
            {
                throw ex;
            }
            catch (TestException2)
            {
            }

            var actualData = new VSIExceptionData();
            _target.WriteToExceptionData(TestClass.MethodInfo1, ex, actualData);

            var expectedData = new VSIExceptionData
            {
                CatchSite = ExceptionWriter.GetProto(TestClass.MethodInfo1)
            };
            var firstExceptionInChain = new VSIExceptionData.Types.Exception
            {
                ExceptionType = ExceptionWriter.GetProto(typeof(TestException2))
            };
            StackFrame stackTraceFrame = new StackTrace(ex, true).GetFrame(0);
            firstExceptionInChain.ExceptionStackTraceFrames.Add(
                new VSIExceptionData.Types.Exception.Types.StackTraceFrame
                {
                    AllowedNamespace = true,
                    Method = ExceptionWriter.GetProto(stackTraceFrame.GetMethod()),
                    Filename = Path.GetFileName(stackTraceFrame.GetFileName()),
                    LineNumber = (uint?) stackTraceFrame.GetFileLineNumber()
                });
            expectedData.ExceptionsChain.Add(firstExceptionInChain);

            Assert.AreEqual(expectedData, actualData);
        }

        [Test]
        public void RecordExceptionChainTooLong()
        {
            var ex = new TestException1("level1",
                                        new TestException1(
                                            "level2",
                                            new TestException1("level3", new TestException2())));
            var actualData = new VSIExceptionData();

            _target.WriteToExceptionData(TestClass.MethodInfo1, ex, actualData);

            Assert.True(ExceptionChainOverflowRecorded(actualData));
        }

        [Test]
        public void WriteExceptionInNotAllowedNamespace()
        {
            var ex = NotAllowedNamespace.Test.ThrowException();
            var actualData = new VSIExceptionData();
            _target.WriteToExceptionData(TestClass.MethodInfo1, ex, actualData);

            var expectedData = new VSIExceptionData
            {
                CatchSite = ExceptionWriter.GetProto(TestClass.MethodInfo1)
            };

            var firstExceptionInChain = new VSIExceptionData.Types.Exception
            {
                ExceptionType = ExceptionWriter.GetProto(typeof(Exception))
            };
            firstExceptionInChain.ExceptionStackTraceFrames.Add(
                new VSIExceptionData.Types.Exception.Types.StackTraceFrame
                {
                    AllowedNamespace = false
                });
            expectedData.ExceptionsChain.Add(firstExceptionInChain);

            Assert.AreEqual(expectedData, actualData);
        }

        static bool ExceptionChainOverflowRecorded(VSIExceptionData data) =>
            data.ExceptionsChain.Count == _maxExceptionsChainLength + 1 && data
                .ExceptionsChain[_maxExceptionsChainLength].ExceptionType.Equals(
                    ExceptionWriter.GetProto(typeof(ExceptionWriter.ChainTooLongException)));

        class TestException1 : Exception
        {
            public TestException1(string message, Exception inner) : base(message, inner)
            {
            }
        }

        class TestException2 : Exception
        {
        }
    }

    class TestClass
    {
        public static readonly MethodInfo MethodInfo1 = typeof(TestClass).GetMethod("TestMethod1");

        public void TestMethod1()
        {
        }
    }
}

namespace NotAllowedNamespace
{
    static class Test
    {
        public static Exception ThrowException()
        {
            try
            {
                throw new Exception("Test");
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
