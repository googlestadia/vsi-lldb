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

using System;
using DebuggerApi;
using DebuggerCommonApi;
using NUnit.Framework;
using NSubstitute;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using YetiVSI.DebugEngine;

namespace YetiVSI.Test.DebugEngine
{
    [TestFixture]
    class DebugBoundBreakpoint_DeletedTests
    {
        private IBoundBreakpoint boundBreakpoint;

        [SetUp]
        public void SetUp()
        {
            SbBreakpointLocation mockBreakpointLocation = Substitute.For<SbBreakpointLocation>();
            mockBreakpointLocation.GetAddress().Returns((SbAddress)null);

            boundBreakpoint = new DebugBoundBreakpoint.Factory(null, null, null)
                .Create(null, mockBreakpointLocation, null, Guid.Empty);
            boundBreakpoint.Delete();
        }

        [Test]
        public void Delete()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED, boundBreakpoint.Delete());
        }

        [Test]
        public void Enable()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED, boundBreakpoint.Enable(1));
        }

        [Test]
        public void GetBreakpointResolution()
        {
            IDebugBreakpointResolution2 breakpointResolution;
            Assert.AreEqual(AD7Constants.E_BP_DELETED,
                boundBreakpoint.GetBreakpointResolution(out breakpointResolution));
            Assert.IsNull(breakpointResolution);
        }

        [Test]
        public void GetHitCount()
        {
            uint hitcount;
            Assert.AreEqual(AD7Constants.E_BP_DELETED, boundBreakpoint.GetHitCount(out hitcount));
            Assert.AreEqual(0, hitcount);
        }

        [Test]
        public void SetHitCount()
        {
            uint hitcount;
            Assert.AreEqual(AD7Constants.E_BP_DELETED, boundBreakpoint.SetHitCount(0));
            Assert.AreEqual(AD7Constants.E_BP_DELETED, boundBreakpoint.GetHitCount(out hitcount));
            Assert.AreEqual(0, hitcount);
        }

        [Test]
        public void SetCondition()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED,
                boundBreakpoint.SetCondition(new BP_CONDITION()));
        }

        [Test]
        public void SetPassCount()
        {
            Assert.AreEqual(AD7Constants.E_BP_DELETED,
                boundBreakpoint.SetPassCount(new BP_PASSCOUNT()));
        }
    }

    [TestFixture]
    class DebugBoundBreakpointTests
    {
        const uint HIT_COUNT = 3;
        const int ID = 1;
        const uint ADDRESS = 0xdeadbeef;
        const string NAME = "DebugBoundBreakpointTests";
        SbAddress mockAddress;
        DebugBoundBreakpoint.Factory boundBreakpointFactory;
        IBoundBreakpoint boundBreakpoint;
        RemoteBreakpoint mockBreakpoint;
        IDebugPendingBreakpoint2 mockPendingBreakpoint;
        SbBreakpointLocation mockBreakpointLocation;
        IDebugBreakpointResolution2 mockBreakpointResolution;
        IDebugProgram2 mockprogram;
        LineEntryInfo lineEntry;
        DebugDocumentContext.Factory mockDocumentContextFactory;
        IDebugDocumentContext2 mockDocumentContext;
        IDebugCodeContext2 mockCodeContext;
        DebugCodeContext.Factory mockCodeContextFactory;
        DebugBreakpointResolution.Factory mockBreakpointResolutionFactory;

        [SetUp]
        public void SetUp()
        {
            string name = "";
            mockBreakpoint = Substitute.For<RemoteBreakpoint>();
            lineEntry = new LineEntryInfo();
            mockPendingBreakpoint = Substitute.For<IDebugPendingBreakpoint2>();
            mockBreakpointLocation = Substitute.For<SbBreakpointLocation>();
            mockAddress = Substitute.For<SbAddress>();
            mockAddress.GetLineEntry().Returns(lineEntry);
            mockBreakpointLocation.GetHitCount().Returns(HIT_COUNT);
            mockBreakpointLocation.GetLoadAddress().Returns(ADDRESS);
            mockBreakpointLocation.GetBreakpoint().Returns(mockBreakpoint);
            mockBreakpointLocation.GetId().Returns(ID);
            mockBreakpointLocation.GetAddress().Returns(mockAddress);
            mockprogram = Substitute.For<IDebugProgram2>();
            mockDocumentContext = Substitute.For<IDebugDocumentContext2>();
            mockDocumentContext.GetName(enum_GETNAME_TYPE.GN_NAME, out name).Returns(
                x =>
                {
                    x[1] = NAME;
                    return VSConstants.S_OK;
                });
            mockBreakpointResolution = Substitute.For<IDebugBreakpointResolution2>();
            mockDocumentContextFactory = Substitute.For<DebugDocumentContext.Factory>();
            mockDocumentContextFactory.Create(lineEntry).Returns(mockDocumentContext);
            mockCodeContext = Substitute.For<IDebugCodeContext2>();
            mockCodeContextFactory = Substitute.For<DebugCodeContext.Factory>();
            mockCodeContextFactory.Create(ADDRESS, NAME,
                mockDocumentContext, Guid.Empty).Returns(mockCodeContext);
            mockBreakpointResolutionFactory =
                Substitute.For<DebugBreakpointResolution.Factory>();
            mockBreakpointResolutionFactory.Create(mockCodeContext, mockprogram).Returns(
                mockBreakpointResolution);
            boundBreakpointFactory = new DebugBoundBreakpoint.Factory(mockDocumentContextFactory,
                mockCodeContextFactory, mockBreakpointResolutionFactory);
            boundBreakpoint = boundBreakpointFactory.Create(
                mockPendingBreakpoint, mockBreakpointLocation, mockprogram, Guid.Empty);
        }

        [Test]
        public void Delete()
        {
            var result = boundBreakpoint.Delete();
            enum_BP_STATE[] state = new enum_BP_STATE[1];
            boundBreakpoint.GetState(state);

            Assert.AreEqual(enum_BP_STATE.BPS_DELETED, state[0]);
            Assert.AreEqual(VSConstants.S_OK, result);
        }

        [Test]
        public void Enable()
        {
            var result = boundBreakpoint.Enable(1);
            enum_BP_STATE[] state = new enum_BP_STATE[1];
            boundBreakpoint.GetState(state);

            Assert.AreEqual(enum_BP_STATE.BPS_ENABLED, state[0]);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockBreakpointLocation.Received().SetEnabled(true);
        }

        [Test]
        public void Disable()
        {
            var result = boundBreakpoint.Enable(0);
            enum_BP_STATE[] state = new enum_BP_STATE[1];
            boundBreakpoint.GetState(state);

            Assert.AreEqual(enum_BP_STATE.BPS_DISABLED, state[0]);
            Assert.AreEqual(VSConstants.S_OK, result);
            mockBreakpointLocation.Received().SetEnabled(false);
        }

        [Test]
        public void GetHitCount()
        {
            uint hitCount;
            var result = boundBreakpoint.GetHitCount(out hitCount);
            Assert.AreEqual(VSConstants.S_OK, result);
            Assert.AreEqual(HIT_COUNT, hitCount);
        }

        [Test]
        public void SetHitCountZero()
        {
            var result = boundBreakpoint.SetHitCount(0);
            Assert.AreEqual(VSConstants.S_OK, result);
            uint hitCount;
            result = boundBreakpoint.GetHitCount(out hitCount);
            Assert.AreEqual(0, hitCount);
        }

        [Test]
        public void SetHitCountTwo()
        {
            var result = boundBreakpoint.SetHitCount(2);
            Assert.AreEqual(VSConstants.S_OK, result);
            uint hitCount;
            result = boundBreakpoint.GetHitCount(out hitCount);
            Assert.AreEqual(2, hitCount);
        }

        [Test]
        public void GetId()
        {
            int outputId = boundBreakpoint.GetId();
            Assert.AreEqual(ID, outputId);
        }

        // Verifies that no breakpoint resolution is created if the breakpoint
        // location doesn't return an address.
        [Test]
        public void GetBreakpointResolutionNullAddress()
        {
            SbBreakpointLocation breakpointLocationNullAddress =
                Substitute.For<SbBreakpointLocation>();
            const SbAddress NULL_ADDRESS = null;
            breakpointLocationNullAddress.GetAddress().Returns(NULL_ADDRESS);
            IDebugBoundBreakpoint2 boundBreakpointNullAddress = boundBreakpointFactory.Create(
                mockPendingBreakpoint, breakpointLocationNullAddress, mockprogram, Guid.Empty);
            IDebugBreakpointResolution2 breakpointResolutionNullAddress;
            Assert.AreEqual(VSConstants.E_FAIL,
                boundBreakpointNullAddress.GetBreakpointResolution(
                    out breakpointResolutionNullAddress));
            Assert.AreEqual(null, breakpointResolutionNullAddress);
        }

        [Test]
        public void GetBreakpointResolutionNullLineEntry()
        {
            mockAddress.GetLineEntry().Returns((LineEntryInfo)null);
            mockBreakpointLocation.GetAddress().Returns(mockAddress);
            mockBreakpointResolutionFactory.Create(mockCodeContext, mockprogram).Returns(
                mockBreakpointResolution);
            mockCodeContextFactory.Create(ADDRESS, "", null, Guid.Empty).Returns(mockCodeContext);
            var boundBreakpointNullLineEntry = boundBreakpointFactory.Create(
                mockPendingBreakpoint, mockBreakpointLocation, mockprogram, Guid.Empty);
            IDebugBreakpointResolution2 output;
            Assert.AreEqual(VSConstants.S_OK,
                boundBreakpointNullLineEntry.GetBreakpointResolution(out output));
            Assert.AreEqual(mockBreakpointResolution, output);
        }

        [Test]
        public void GetBreakpointResolution()
        {
            IDebugBreakpointResolution2 breakpointResolution;
            Assert.AreEqual(VSConstants.S_OK,
                boundBreakpoint.GetBreakpointResolution(out breakpointResolution));
            Assert.AreEqual(mockBreakpointResolution, breakpointResolution);
        }

        [Test]
        public void SetConditionWhenTrue()
        {
            BP_CONDITION condition;
            condition.styleCondition = enum_BP_COND_STYLE.BP_COND_WHEN_TRUE;
            condition.bstrCondition = "true";
            condition.bstrContext = null;
            condition.pThread = null;
            condition.nRadix = 10;
            Assert.AreEqual(VSConstants.S_OK, boundBreakpoint.SetCondition(condition));
            mockBreakpointLocation.Received(1).SetCondition("true");
        }

        [Test]
        public void SetConditionNone()
        {
            BP_CONDITION condition;
            condition.styleCondition = enum_BP_COND_STYLE.BP_COND_NONE;
            condition.bstrCondition = null;
            condition.bstrContext = null;
            condition.pThread = null;
            condition.nRadix = 0;
            Assert.AreEqual(VSConstants.S_OK, boundBreakpoint.SetCondition(condition));
            mockBreakpointLocation.Received(1).SetCondition("");
        }

        [Test]
        public void GetPendingBreakpoint()
        {
            IDebugPendingBreakpoint2 pendingBreakpoint;
            Assert.AreEqual(VSConstants.S_OK, boundBreakpoint.GetPendingBreakpoint(
                out pendingBreakpoint));
            Assert.AreEqual(mockPendingBreakpoint, pendingBreakpoint);
        }

        [Test]
        public void SetPassCountEqualOrGreater()
        {
            const uint PASS_COUNT = 5;
            BP_PASSCOUNT passCount;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL_OR_GREATER;
            passCount.dwPassCount = PASS_COUNT;
            Assert.AreEqual(VSConstants.S_OK, boundBreakpoint.SetPassCount(passCount));
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - HIT_COUNT - 1);
        }

        [Test]
        public void SetPassCountEqual()
        {
            const uint PASS_COUNT = 5;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            Assert.AreEqual(VSConstants.S_OK, boundBreakpoint.SetPassCount(passCount));
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - HIT_COUNT - 1);
        }

        [Test]
        public void SetPassCountNone()
        {
            BP_PASSCOUNT passCount;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE;
            passCount.dwPassCount = 0;
            Assert.AreEqual(VSConstants.S_OK, boundBreakpoint.SetPassCount(passCount));
            mockBreakpointLocation.Received(1).SetIgnoreCount(0);
        }

        [Test]
        public void SetPassCountMod()
        {
            const uint PASS_COUNT = 2;
            BP_PASSCOUNT passCount;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD;
            passCount.dwPassCount = PASS_COUNT;
            mockBreakpoint.GetHitCount().Returns(HIT_COUNT);
            Assert.AreEqual(VSConstants.S_OK, boundBreakpoint.SetPassCount(passCount));
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT -
                HIT_COUNT % PASS_COUNT - 1);
        }

        [Test]
        public void EnableWhenDisabledByPassCount()
        {
            const uint PASS_COUNT = 5;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.Received(1).SetEnabled(true);
            mockBreakpoint.GetHitCount().Returns(1u);
            boundBreakpoint.OnHit();
            mockBreakpointLocation.Received(1).SetEnabled(false);
            boundBreakpoint.Enable(1);
            mockBreakpointLocation.Received(1).SetEnabled(true);
        }

        [Test]
        public void SetPassCountEqualSmall()
        {
            const uint PASS_COUNT = 3;
            const uint HIT_COUNT = 4;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            mockBreakpoint.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.DidNotReceive().SetIgnoreCount(1);
            mockBreakpointLocation.Received(1).SetEnabled(false);
        }

        [Test]
        public void SetPassCountEqualEqual()
        {
            const uint PASS_COUNT = 3;
            const uint HIT_COUNT = 3;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            mockBreakpoint.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.DidNotReceive().SetIgnoreCount(0);
            mockBreakpointLocation.Received(1).SetEnabled(false);
        }

        [Test]
        public void SetPassCountEqualLarge()
        {
            const uint PASS_COUNT = 3;
            const uint HIT_COUNT = 1;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            mockBreakpointLocation.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.Received(1).SetIgnoreCount(1);
            mockBreakpointLocation.DidNotReceive().SetEnabled(false);
        }

        [Test]
        public void SetPassCountReset()
        {
            const uint PASS_COUNT = 3;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            boundBreakpoint.SetPassCount(passCount);
            BP_PASSCOUNT passCountNone;
            passCountNone.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE;
            passCountNone.dwPassCount = 0;
            boundBreakpoint.SetPassCount(passCountNone);
            mockBreakpointLocation.Received(1).SetIgnoreCount(0);
            mockBreakpointLocation.Received(2).SetEnabled(true);
        }

        [Test]
        public void ResetPassCountEqualAfterHit()
        {
            const uint PASS_COUNT = 4;
            const uint HIT_COUNT = 4;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.Received(1).SetEnabled(true);
            mockBreakpoint.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.OnHit();
            mockBreakpointLocation.Received(1).SetEnabled(false);
            BP_PASSCOUNT passCountNone;
            passCountNone.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_NONE;
            passCountNone.dwPassCount = 0;
            boundBreakpoint.SetPassCount(passCountNone);
            mockBreakpointLocation.Received(2).SetEnabled(true);
        }

        [Test]
        public void OnHitPassCountEqual()
        {
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = 5;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            boundBreakpoint.SetPassCount(passCount);
            boundBreakpoint.OnHit();
            mockBreakpointLocation.Received(1).SetEnabled(false);
        }

        [Test]
        public void OnHitPassCountMod()
        {
            const uint PASS_COUNT = 3;
            const uint HIT_COUNT = 1;
            mockBreakpointLocation.GetHitCount().Returns(HIT_COUNT);
            BP_PASSCOUNT passCount;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_MOD;
            passCount.dwPassCount = PASS_COUNT;
            boundBreakpoint.SetPassCount(passCount);
            boundBreakpoint.OnHit();
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - 1);
        }

        [Test]
        public void SetPassCountEqualSmallResetLarge()
        {
            const uint PASS_COUNT = 3;
            const uint HIT_COUNT = 1;
            const uint NEW_HIT_COUNT = 5;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            mockBreakpointLocation.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - HIT_COUNT - 1);
            boundBreakpoint.SetHitCount(NEW_HIT_COUNT);
            mockBreakpointLocation.Received(1).SetEnabled(false);
        }

        [Test]
        public void SetPassCountEqualSmallResetEqual()
        {
            const uint PASS_COUNT = 3;
            const uint HIT_COUNT = 1;
            const uint NEW_HIT_COUNT = 3;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            mockBreakpointLocation.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - HIT_COUNT - 1);
            boundBreakpoint.SetHitCount(NEW_HIT_COUNT);
            mockBreakpointLocation.Received(1).SetEnabled(false);
        }

        [Test]
        public void SetPassCountEqualSmallResetSmall()
        {
            const uint PASS_COUNT = 5;
            const uint HIT_COUNT = 3;
            const uint NEW_HIT_COUNT = 1;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            mockBreakpoint.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - HIT_COUNT - 1);
            mockBreakpointLocation.ClearReceivedCalls();
            boundBreakpoint.SetHitCount(NEW_HIT_COUNT);
            mockBreakpointLocation.Received(1).SetEnabled(true);
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - NEW_HIT_COUNT - 1);
        }

        [Test]
        public void SetPassCountEqualLargeResetSmall()
        {
            const uint PASS_COUNT = 3;
            const uint HIT_COUNT = 5;
            const uint NEW_HIT_COUNT = 0;
            BP_PASSCOUNT passCount;
            passCount.dwPassCount = PASS_COUNT;
            passCount.stylePassCount = enum_BP_PASSCOUNT_STYLE.BP_PASSCOUNT_EQUAL;
            mockBreakpoint.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetPassCount(passCount);
            mockBreakpointLocation.Received(1).SetEnabled(false);
            mockBreakpointLocation.ClearReceivedCalls();
            boundBreakpoint.SetHitCount(NEW_HIT_COUNT);
            mockBreakpointLocation.Received(1).SetEnabled(true);
            mockBreakpointLocation.Received(1).SetIgnoreCount(PASS_COUNT - NEW_HIT_COUNT - 1);
        }

        [Test]
        public void SetHitCountAndOnHit()
        {
            const uint HIT_COUNT = 5;
            const uint NEW_HIT_COUNT = 1;
            mockBreakpointLocation.GetHitCount().Returns(HIT_COUNT);
            boundBreakpoint.SetHitCount(NEW_HIT_COUNT);

            mockBreakpointLocation.GetHitCount().Returns(HIT_COUNT + 1);
            boundBreakpoint.OnHit();

            uint hitCount;
            var result = boundBreakpoint.GetHitCount(out hitCount);
            Assert.AreEqual(VSConstants.S_OK, result);
            Assert.AreEqual(NEW_HIT_COUNT + 1, hitCount);
        }
    }
}
