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
using YetiCommon.Logging;

namespace YetiCommon.Cloud
{
    // Adapts NLog.ILogger to Grpc.Core.Logging.ILogger
    class GrpcLogger : Grpc.Core.Logging.ILogger
    {
        NLog.ILogger logger;
        Type forType;

        public GrpcLogger(Type forType = null)
        {
            this.forType = forType;
            string forTypeString = "Grpc";

            if (forType != null)
            {
                var namespaceStr = forType.Namespace ?? "";
                if (namespaceStr.Length > 0)
                {
                    namespaceStr += ".";
                }
                forTypeString = namespaceStr + forType.Name + " ";
            }
            logger = YetiLog.GetLogger(forTypeString);
        }

        public void Debug(string message)
        {
            logger.Debug(message);
        }

        public void Debug(string format, params object[] formatArgs)
        {
            logger.Debug(format, formatArgs);
        }

        public void Error(string message)
        {
            logger.Error(message);
        }

        public void Error(Exception exception, string message)
        {
            logger.Error(exception, message);
        }

        public void Error(string format, params object[] formatArgs)
        {
            logger.Error(format, formatArgs);
        }

        public Grpc.Core.Logging.ILogger ForType<T>()
        {
            Type type = typeof(T);
            if (type == forType)
            {
                return this;
            }
            return new GrpcLogger(type);
        }

        public void Info(string message)
        {
            logger.Info(message);
        }

        public void Info(string format, params object[] formatArgs)
        {
            logger.Info(format, formatArgs);
        }

        public void Warning(string message)
        {
            logger.Warn(message);
        }

        public void Warning(Exception exception, string message)
        {
            logger.Warn(exception, message);
        }

        public void Warning(string format, params object[] formatArgs)
        {
            logger.Warn(format, formatArgs);
        }
    }
}
