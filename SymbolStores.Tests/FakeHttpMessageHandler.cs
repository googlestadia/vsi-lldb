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

﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SymbolStores.Tests
{
    // Fake http message handler, which can be used to test code involving HttpClient without
    // involving the network.
    // Any request for a URI that does not have a primed response will return a 404 status code.
    // TODO: Move this class to a more general location.
    // Since this class may be adapted for more general use, it should not be overfit to be
    // overly symbol store specifc.
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        // Map of content to be returned for specific urls
        public IDictionary<Uri, byte[]> ContentMap = new Dictionary<Uri, byte[]>();

        // Map of exceptions that should be thrown when specific urls are requested
        public IDictionary<Uri, Exception> ExceptionMap = new Dictionary<Uri, Exception>();

        // Whether http HEAD requests should be supported, or return status 405.
        public bool SupportsHeadRequests { get; set; } = true;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Exception exception;
            if (ExceptionMap.TryGetValue(request.RequestUri, out exception))
            {
                throw exception;
            }

            byte[] responseContent;
            if (ContentMap.TryGetValue(request.RequestUri, out responseContent))
            {
                if (request.Method == HttpMethod.Head)
                {
                    if (SupportsHeadRequests)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            RequestMessage = request,
                        });
                    }
                    else
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                        {
                            ReasonPhrase = "HEAD requests not supported",
                            Content = new StringContent(""),
                            RequestMessage = request,
                        };
                        response.Content.Headers.Allow.Add("GET");
                        return Task.FromResult(response);
                    }
                }
                else
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(responseContent),
                        RequestMessage = request,
                    });
                }
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    ReasonPhrase = "Not found",
                    RequestMessage = request,
                });
            }
        }
    }
}
