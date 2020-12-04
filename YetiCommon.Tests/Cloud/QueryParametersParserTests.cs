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

using NUnit.Framework;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using YetiCommon.Cloud;
using static YetiCommon.Tests.Cloud.LaunchRequestParsingTestData;

namespace YetiCommon.Tests.Cloud
{
    [TestFixture]
    partial class QueryParametersParserTests
    {
        QueryParametersParser _target;

        [SetUp]
        public void Setup()
        {
            _target = new QueryParametersParser();
        }

        [TestCase("as=234", new[] { "as", "234" }, TestName = "OneParameter")]
        [TestCase("?p=12&s='str'", new[] { "p", "12", "s", "'str'" }, TestName = "QuestionMark")]
        [TestCase("a1=1&a2=4.7a&a2=ff,t&a1=g,th,y&a1=8&b=34",
                  new[] { "a1", "1,g,th,y,8", "a2", "4.7a,ff,t", "b", "34" },
                  TestName = "MultipleAssignment")]
        [TestCase("param=%3D%3B%C3%BC%26%3F%2C%D1%8F%D0%B7%D1%85%D1%97%25",
                  new[] { "param", "=;ü&?,язхї%" }, TestName = "SpecialCharacters")]
        [TestCase("Param=SomeVal&param=other&P1=74", new[] { "Param", "SomeVal,other", "P1", "74" },
                  TestName = "UpperCase")]
        public void ParametersToDictionarySuccess(string input, string[] output)
        {
            ConfigStatus status =
                _target.ParametersToDictionary(input, out IDictionary<string, string> result);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.That(result.Count, Is.EqualTo(output.Length / 2));
            for (int i = 0; i < output.Length / 2; ++i)
            {
                Assert.That(result[output[i * 2]], Is.EqualTo(output[i * 2 + 1]));
            }
        }

        [TestCase("Plain text", TestName = "PlainText")]
        [TestCase("=", TestName = "EqualSign")]
        [TestCase("?=val&=&t&a=1", TestName = "SeveralInvalidValues")]
        [TestCase("?param=val&=val&param1=val1", TestName = "SeveralValidValues")]
        public void ParametersToDictionaryFailure(string input)
        {
            ConfigStatus status =
                _target.ParametersToDictionary(input, out IDictionary<string, string> result);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.WarningMessages.Count, Is.EqualTo(1));
            Assert.That(
                status.WarningMessage.Contains(
                    "The 'Custom Query Parameters' value is in a wrong format"));
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetFinalQueryStringWithWarning()
        {
            Dictionary<string, string> queryParameters = AllValidQueryParams;

            ConfigStatus status =
                _target.GetFinalQueryString(queryParameters, out string queryString);

            Assert.That(status.IsWarningLevel, Is.EqualTo(true));
            Assert.That(status.WarningMessages.Count, Is.EqualTo(1));
            Assert.That(status.AllMessages.Count, Is.EqualTo(1));
            Assert.That(
                status.WarningMessage.Contains("The following query parameters will be ignored"));

            foreach (KeyValuePair<string, string> ignoreQueryParam in IgnoreQueryParams)
            {
                Assert.That(
                    status.WarningMessage.Contains($"'{ignoreQueryParam.Key}'"));
            }

            NameValueCollection parsedQuery = HttpUtility.ParseQueryString(queryString);
            Dictionary<string, string> queryDict =
                parsedQuery.AllKeys.ToDictionary(k => k, k => parsedQuery.Get(k));
            Assert.That(queryDict, Is.EqualTo(OtherQueryParams));
        }

        [Test]
        public void GetFinalQueryStringNoWarning()
        {
            Dictionary<string, string> queryParameters = AllValidQueryParams;
            foreach (KeyValuePair<string, string> ignoreQueryParam in IgnoreQueryParams)
            {
                queryParameters.Remove(ignoreQueryParam.Key);
            }

            ConfigStatus status =
                _target.GetFinalQueryString(queryParameters, out string queryString);

            Assert.That(status.IsOk, Is.EqualTo(true));
            NameValueCollection parsedQuery = HttpUtility.ParseQueryString(queryString);
            Dictionary<string, string> queryDict =
                parsedQuery.AllKeys.ToDictionary(k => k, k => parsedQuery.Get(k));
            Assert.That(queryDict, Is.EqualTo(OtherQueryParams));
        }

        [Test]
        public void GetFinalQueryStringSpecialCharacters()
        {
            var queryParameters = new Dictionary<string, string>
                { { "param", " =;ü&?,язхї%" }, { "param2", "=&ö?chars" } };

            ConfigStatus status =
                _target.GetFinalQueryString(queryParameters, out string queryString);

            Assert.That(status.IsOk, Is.EqualTo(true));
            Assert.That(queryString,
                        Is.EqualTo("param=%20%3D%3B%C3%BC%26%3F%2C%D1%8F%D0%B7%D1%85%D1%97%25" +
                                   "&param2=%3D%26%C3%B6%3Fchars"));
        }
    }
}
