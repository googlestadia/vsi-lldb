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

﻿using GgpGrpc.Cloud.Interceptors;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading;
using System.Threading.Tasks;

namespace YetiCommon.PerformanceTracing
{
    /// <summary>
    /// Send information about GRPC calls to a TracingLogger.
    /// </summary>
    public class TracingGrpcInterceptor : UnaryInterceptorBase
    {
        readonly ITracingLogger traceLogger;
        readonly ITimeSource timeSource;

        public TracingGrpcInterceptor(ITracingLogger traceLogger, ITimeSource timeSource)
        {
            this.traceLogger = traceLogger;
            this.timeSource = timeSource;
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            long startTicks = timeSource.GetTimestampTicks();
            var tid = Thread.CurrentThread.ManagedThreadId;

            var response = continuation(request, context);

            long endTicks = timeSource.GetTimestampTicks();
            traceLogger.TraceEvent(
                context.Method.Name,
                EventType.Sync,
                request.GetType(),
                timeSource.GetDurationUs(startTicks, endTicks),
                timeSource.ConvertTicksToUs(startTicks),
                tid);

            return response;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest,
            TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            long startTicks = timeSource.GetTimestampTicks();

            return HandleResponseAsync(continuation(request, context),
                (response) => TracingResponseAsync(request, context, response, startTicks));
        }

        private async Task<TResponse> TracingResponseAsync<TRequest, TResponse>(
            TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
            Task<TResponse> asyncResponse, long startTicks)
              where TRequest : class
              where TResponse : class
        {
            var response = await asyncResponse;

            var tid = Thread.CurrentThread.ManagedThreadId;
            long endTicks = timeSource.GetTimestampTicks();
            traceLogger.TraceEvent(
                context.Method.Name,
                EventType.Async,
                request.GetType(),
                timeSource.GetDurationUs(startTicks, endTicks),
                timeSource.ConvertTicksToUs(startTicks),
                tid);
            return response;
        }
    }
}
