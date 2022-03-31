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

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using TestsCommon.TestSupport;
using YetiVSI.DebugEngine;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.Test.DebugEngine.Variables
{
    [TestFixture]
    class VsExpressionCreatorTests
    {
        VsExpressionCreator _expressionCreator;
        Func<string, Task<uint>> _evaluateSize;

        [SetUp]
        public void SetUp()
        {
            _expressionCreator = new VsExpressionCreator();
            _evaluateSize = Substitute.For<Func<string, Task<uint>>>();

            // Size specifier expressions resolve to zero by default.
            _evaluateSize(Arg.Any<string>()).Returns(Task.FromResult(0U));
        }

        [Test]
        public async Task CreateWhenExpressionIsEmptyAsync()
        {
            var expression = await _expressionCreator.CreateAsync("", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenSpecifierDoesntExistAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myExpression", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenSpecifierIsEmptyAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myExpression,", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenSpecifierExistsAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myExpression,x", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("x"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenSuSpecifierAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myExpression,su", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("su"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenSpecifierIsNumberAsync()
        {
            var expression = await _expressionCreator.CreateAsync("arr,42", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("42"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenCommaInTheExpressionAsync()
        {
            var expression = await _expressionCreator.CreateAsync("foo(1, 2)", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("foo(1, 2)"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenCommaExpressionAsync()
        {
            var expression = await _expressionCreator.CreateAsync("v1, v2", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("v1, v2"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenMultipleCommasExistAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression(a,b),x", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression(a,b)"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("x"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenSpaceAfterCommaAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myExpression, x", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("x"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWhenSpaceAfterSpecifierAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myExpression,x ", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("x"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithSpacesAndExpressionSpecifierAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression, [42] ", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[42]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("42"));
        }

        [Test]
        public async Task CreateWithExpressionSizeSpecifierAndFormatterAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression,[42]s", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[42]s"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("42"));
        }

        [Test]
        public async Task CreateWithExpressionAndSpecifierWithNestedBracketsAndFormatterAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression,[a[i]]x", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[a[i]]x"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("a[i]"));
        }

        [Test]
        public async Task
        CreateWithIndexedExpressionAndSpecifierWithNestedBracketsAndFormatterAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression[i],[a[i]]!s32", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression[i]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[a[i]]!s32"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("a[i]"));
        }

        [Test]
        public async Task CreateWithIndexedExpressionAndSpecifierWithNestedCommaAndFormatterAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression[i],[f(a, b)]!!o", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression[i]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[f(a, b)]!!o"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("f(a, b)"));
        }

        [Test]
        public async Task CreateWithCommentInSizeAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression,[1/*],[*/]su", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[1/*],[*/]su"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("1/*],[*/"));
        }

        [Test]
        public async Task CreateWithDoubleQuoteInSizeAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression,[f(\"],[\")]su", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[f(\"],[\")]su"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("f(\"],[\")"));
        }

        [Test]
        public async Task CreateWithCommaInBracketsAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("myExpression[a[i], [a[i]]]s8", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myExpression[a[i], [a[i]]]s8"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithDeserialize_WhenSpaceAfterCommaAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myExpression, x", _evaluateSize);
            Assert.That(expression.ToString(), Is.EqualTo("myExpression,x"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithDeserialize_WhenNoSpaceAfterCommaAsync(
            [Values("expression,x", "expression(a,b),x", "")] string expressionStr)
        {
            var expression = await _expressionCreator.CreateAsync(expressionStr, _evaluateSize);
            Assert.That(expression.ToString(), Is.EqualTo(expressionStr));
        }

        [Test]
        public async Task CreateForArrayIndexExpressionAsync()
        {
            var expression = await _expressionCreator.CreateAsync("arr[0]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr[0]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithFormatterAsExpressionAsync()
        {
            var expression = await _expressionCreator.CreateAsync("myvar,[1 + 2]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("myvar"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[1 + 2]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke("1 + 2");
        }

        [Test]
        public async Task CreateWithCommaInTheCommentAsync()
        {
            var expression = await _expressionCreator.CreateAsync("1 + /* , */ 2,x", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1 + /* , */ 2"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("x"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithCommaInTheCommentInFromatterAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("1 + 2,[x /* ,[ */ + y]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1 + 2"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[x /* ,[ */ + y]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("x /* ,[ */ + y"));
        }

        [Test]
        public async Task CreateWithBracketInTheCommentInExpressionAsync()
        {
            var expression = await _expressionCreator.CreateAsync("arr[ /* ,[*/ 0]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr[ /* ,[*/ 0]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithNestedBracketsAsync()
        {
            var expression = await _expressionCreator.CreateAsync("arr,[vec[1]]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[vec[1]]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("vec[1]"));
        }

        [Test]
        public async Task CreateWithCommasTemplatesAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("Stack<int,int>::st(5)", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("Stack<int,int>::st(5)"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithSeveralBracketsAsync()
        {
            var expression = await _expressionCreator.CreateAsync("arr,[][]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr,[][]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithBracketsInStringsInFormatterAsync()
        {
            var expression = await _expressionCreator.CreateAsync("1,[a\",[b\"]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[a\",[b\"]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("a\",[b\""));
        }

        [Test]
        public async Task CreateWithBracketsInFormatInExpressionAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("arr[foo(\",[\")],[1]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr[foo(\",[\")]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[1]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("1"));
        }

        [Test]
        public async Task CreateWithBracketsInCommentsInFormatterAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("1,[a + /*,[ [ ]*/b]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[a + /*,[ [ ]*/b]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("a + /*,[ [ ]*/b"));
        }

        [Test]
        public async Task CreateWithBracketsInCommentsInExpressionAsyncAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("1 /*,[ [ ]*/,[[[1]]]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1 /*,[ [ ]*/"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[[[1]]]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("[[1]]"));
        }

        [Test]
        public async Task CreateWithEscapedQuotesAsync()
        {
            // 1, [" [\" "]
            var expression = await _expressionCreator.CreateAsync("1,[\"[\\\" \"]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[\"[\\\" \"]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("\"[\\\" \""));
        }

        [Test]
        public async Task CreateWithSingleQuotesAsync()
        {
            var expression = await _expressionCreator.CreateAsync("1,['\"']", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("['\"']"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("'\"'"));
        }

        [Test]
        public async Task CreateWithSingleQuotesInSingleQuotesAsync()
        {
            var expression = await _expressionCreator.CreateAsync("1,['\\'']", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("['\\'']"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("'\\''"));
        }

        [Test]
        public async Task CreateWithNonAsciiAsync()
        {
            var expression = await _expressionCreator.CreateAsync("1,[\"日本語\"]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[\"日本語\"]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("\"日本語\""));
        }

        [Test]
        public async Task TestCornerCaseWithEmptyBracketsAsync()
        {
            var expression = await _expressionCreator.CreateAsync("[]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("[]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task TestCornerCaseWithNonClosedBracketAsync()
        {
            var expression = await _expressionCreator.CreateAsync("]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task TestWithNotClosedCommentAsync()
        {
            var expression = await _expressionCreator.CreateAsync("arr,[]/*]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr,[]/*]"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        [Ignore("TODO")]
        public async Task CreateWithRawStringsAsync()
        {
            var expression = await _expressionCreator.CreateAsync("1,[R\"(,[\")\"]", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("1"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[R\"(,[\")\"]"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);
            await _evaluateSize.Received(1).Invoke(Arg.Is("R\"(,[\")\""));
        }

        [Test]
        public async Task CreateWithExpandAsync()
        {
            var expression = await _expressionCreator.CreateAsync("arr,expand(3)", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("arr"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("expand(3)"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithViewAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("arr,view(simple)", _evaluateSize);
            Assert.That(expression.Value, Is.EqualTo("arr"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("view(simple)"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithRawFormatSpecifierAsync()
        {
            var expression = await _expressionCreator.CreateAsync("test,!", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("test"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("!"));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public async Task CreateWithInvalidRawFormatSpecifierAsync()
        {
            var expression =
                await _expressionCreator.CreateAsync("test,!unsupportedSpecifier", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("test,!unsupportedSpecifier"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo(""));
            Assert.That(expression.FormatSpecifier.Size, Is.Null);
            await _evaluateSize.DidNotReceiveWithAnyArgs().Invoke(Arg.Any<string>());
        }

        [Test]
        public void LogErrorIfEvaluationFails()
        {
            LogSpy traceLogSpy = new LogSpy();
            traceLogSpy.Attach();

            var exception = new ExpressionEvaluationFailed("undeclared identifier 'field'");
            _evaluateSize(Arg.Any<string>()).Throws(exception);

            var receivedException = Assert.ThrowsAsync<ExpressionEvaluationFailed>(
                async () =>
                    await _expressionCreator.CreateAsync("test,[field + 42]", _evaluateSize));
            Assert.That(receivedException.Message, Is.EqualTo("undeclared identifier 'field'"));
        }

        [Test]
        public void LogErrorIfEvaluationFailsWithValidSuffix()
        {
            LogSpy traceLogSpy = new LogSpy();
            traceLogSpy.Attach();

            var exception = new ExpressionEvaluationFailed("undeclared identifier 'bad_expr'");
            _evaluateSize(Arg.Any<string>()).Throws(exception);

            var receivedException = Assert.ThrowsAsync<ExpressionEvaluationFailed>(
                async () =>
                    await _expressionCreator.CreateAsync("test,[bad_expr]!sub", _evaluateSize));

            Assert.That(receivedException.Message, Is.EqualTo("undeclared identifier 'bad_expr'"));
        }

        [Test]
        public async Task DoNotLogErrorIfEvaluationSucceedAsync()
        {
            LogSpy traceLogSpy = new LogSpy();
            traceLogSpy.Attach();

            var expression = await _expressionCreator.CreateAsync("test,[expr]s", _evaluateSize);

            Assert.That(expression.Value, Is.EqualTo("test"));
            Assert.That(expression.FormatSpecifier.Expression, Is.EqualTo("[expr]s"));
            Assert.That(expression.FormatSpecifier.Size, Is.Not.Null);

            Assert.That(traceLogSpy.GetOutput(),
                        Does.Not.Contain("Failed to resolve size format expression"));
        }
    }
}
