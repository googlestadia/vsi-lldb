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

﻿using Castle.DynamicProxy;
using NLog;
using NLog.Targets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using YetiCommon.CastleAspects;

namespace YetiCommon.Tests.CastleAspects
{
    public interface ITargetClass
    {
        void Method1();
        void Method2();
        void Method3();
        void Throw();
        bool TryGetArray(out ITargetClass[] arr);
        bool TryGetSingle(out ITargetClass single);
        bool TryGetArrayAndString(out ITargetClass[] arr, out string str);
    }

    class ObjectIDGeneratorStub : ObjectIDGenerator
    {
        readonly long id;
        HashSet<object> objects = new HashSet<object>();

        public ObjectIDGeneratorStub(long id)
        {
            this.id = id;
        }

        public override long GetId(object obj, out bool firstTime)
        {
            firstTime = objects.Add(obj);
            return id;
        }
    }

    class TargetClass : ITargetClass
    {
        readonly ITargetClass other;

        public TargetClass(ITargetClass other = null)
        {
            this.other = other;
        }

        public void Method1() => other?.Method2();

        public void Method2() => other?.Method3();

        public void Method3() { }

        public void Throw()
        {
            throw new TestException();
        }

        public bool TryGetArray(out ITargetClass[] arr)
        {
            arr = new[] { other, other };
            return true;
        }

        public bool TryGetSingle(out ITargetClass single)
        {
            single = other;
            return true;
        }

        public bool TryGetArrayAndString(out ITargetClass[] arr, out string str)
        {
            arr = new[] { other, other };
            str = "dummy";
            return true;
        }
    }

    class TestException : Exception { }

    [TestFixture]
    class CallSequenceLoggingAspectTests
    {
        ObjectIDGenerator idGenerator;
        ProxyGenerator proxyGenerator;
        MemoryTarget logTarget;

        // Object under test.
        CallSequenceLoggingAspect sequenceAspect;

        [SetUp]
        public void SetUp()
        {
            idGenerator = new ObjectIDGeneratorStub(17);
            proxyGenerator = new ProxyGenerator();

            logTarget = new MemoryTarget();
            logTarget.Layout = "${message}";
            // Using LogLevel.Trace to get all logs.
            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(logTarget, LogLevel.Trace);
            sequenceAspect = new CallSequenceLoggingAspect(
                idGenerator, LogManager.GetLogger("test"));
        }

        [Test]
        public void LogsIncludeObjectId()
        {
            ITargetClass proxy = GetProxy();
            proxy.Method1();

            var logs = string.Join(Environment.NewLine, logTarget.Logs);
            Assert.That(logs, Does.Contain("(id=17"));
        }

        [Test]
        public void LogsIncludeComments()
        {
            ITargetClass proxy = GetProxy();
            proxy.Method1();

            var logs = string.Join(Environment.NewLine, logTarget.Logs);
            Assert.That(logs, Does.Contain("# Call Section"));
        }

        [Test]
        public void MultipleCalls()
        {
            ITargetClass proxy3 = GetProxy();
            ITargetClass proxy2 = GetProxy(proxy3);
            ITargetClass proxy1 = GetProxy(proxy2);

            proxy1.Method1();

            CollectionAssert.AreEqual(new[]
            {
                "VS -> YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.Method1",
                "YetiCommon.Tests.CastleAspects.TargetClass -> " +
                    "YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.Method2 [color=\"red\"]",
                "YetiCommon.Tests.CastleAspects.TargetClass -> " +
                    "YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.Method3 [color=\"red\"]",
            }, GetNormalizedLogs());
        }

        [Test]
        public void SingleProxyReturned()
        {
            ITargetClass proxy2 = GetProxy();
            ITargetClass proxy1 = GetProxy(proxy2);
            ITargetClass ignored = new TargetClass();

            proxy1.TryGetSingle(out ignored);

            CollectionAssert.AreEqual(new[]
            {
                "VS -> YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.TryGetSingle",
                "YetiCommon.Tests.CastleAspects.TargetClass --> VS: " +
                    "YetiCommon.Tests.CastleAspects.TargetClass",
            }, GetNormalizedLogs());
        }

        [Test]
        public void ArrayProxyReturned()
        {
            ITargetClass proxy2 = GetProxy();
            ITargetClass proxy1 = GetProxy(proxy2);
            ITargetClass[] ignored;

            proxy1.TryGetArray(out ignored);

            CollectionAssert.AreEqual(new[]
            {
                "VS -> YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.TryGetArray",
                "YetiCommon.Tests.CastleAspects.TargetClass --> VS: " +
                    "\\[YetiCommon.Tests.CastleAspects.TargetClass, " +
                    "YetiCommon.Tests.CastleAspects.TargetClass]",
            }, GetNormalizedLogs());
        }

        [Test]
        public void ThrowsException()
        {
            ITargetClass proxy = GetProxy();

            Assert.Throws<TestException>(() => proxy.Throw());
            // Calls method3 to ensure the previous exception didn't leave the stack
            // in an inconsistent state.
            proxy.Method3();

            CollectionAssert.AreEqual(new[]
            {
                "VS -> YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.Throw",
                "YetiCommon.Tests.CastleAspects.TargetClass --> " +
                    "VS: YetiCommon.Tests.CastleAspects.TestException",
                "VS -> YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.Method3",
            }, GetNormalizedLogs());
        }

        [Test]
        public void ReturnsNonProxiedValue()
        {
            ITargetClass undecoratedTgt = new TargetClass();
            ITargetClass proxy = GetProxy(undecoratedTgt);
            ITargetClass[] ignoredArr;
            string dummyStr;

            proxy.TryGetArrayAndString(out ignoredArr, out dummyStr);

            CollectionAssert.AreEqual(new[]
            {
                "VS -> YetiCommon.Tests.CastleAspects.TargetClass: " +
                    "ITargetClass.TryGetArrayAndString",
                "YetiCommon.Tests.CastleAspects.TargetClass --> " +
                    "VS: \\[{YetiCommon.Tests.CastleAspects.TargetClass}, " +
                    "{YetiCommon.Tests.CastleAspects.TargetClass}], " +
                    "{System.String} {value=dummy}",
            }, GetNormalizedLogs());
        }

        ITargetClass GetProxy(ITargetClass other=null) =>
            proxyGenerator.CreateInterfaceProxyWithTarget<ITargetClass>(
                new TargetClass(other), sequenceAspect);

        long GetId(object obj)
        {
            bool ignore;
            return obj == null ? 0 : idGenerator.GetId(obj, out ignore);
        }

        /// <summary>
        /// Normalizes the logs by stripping
        ///   - empty lines
        ///   - participant lines
        ///   - comment lines
        ///   - object id text
        /// </summary>
        IEnumerable<string> GetNormalizedLogs()
        {
            foreach (var logLine in logTarget.Logs)
            {
                if (!string.IsNullOrEmpty(logLine) &&
                    !logLine.StartsWith("#") &&
                    !logLine.StartsWith("participant"))
                {
                    var processedLogLine = logLine.Replace(" (id=17)", "");
                    yield return processedLogLine;
                }
            }
            yield break;
        }
    }
}
