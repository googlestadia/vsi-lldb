using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Metrics.Shared
{
    public interface IExceptionWriter
    {
        void WriteToExceptionData(MethodBase callSite, Exception ex, VSIExceptionData data);
    }

    public class ExceptionWriter : IExceptionWriter
    {
        const int _defaultMaxExceptionsChainLength = 10;
        const int _defaultMaxStackTraceFrames = 100;
        readonly string[] _namespaceAllowList;
        readonly int _maxExceptionsChainLength;
        readonly int _maxStackTraceFrames;

        public ExceptionWriter(string[] namespaceAllowList,
                               int maxExceptionsChainLength = _defaultMaxExceptionsChainLength,
                               int maxStackTraceFrames = _defaultMaxStackTraceFrames)
        {
            _namespaceAllowList = namespaceAllowList;
            _maxExceptionsChainLength = maxExceptionsChainLength;
            _maxStackTraceFrames = maxStackTraceFrames;
        }

        public void WriteToExceptionData(MethodBase callSite, Exception ex, VSIExceptionData data)
        {
            data.CatchSite = GetProto(callSite);
            RecordExceptionChain(ex, data);
        }

        void RecordExceptionChain(Exception ex, VSIExceptionData data)
        {
            for (uint i = 0; i < _maxExceptionsChainLength && ex != null; i++)
            {
                var exData = new VSIExceptionData.Types.Exception();
                exData.ExceptionType = GetProto(ex.GetType());
                exData.ExceptionStackTraceFrames.AddRange(GetStackTraceFrames(ex));

                // TODO: record the exception stack trace.
                data.ExceptionsChain.Add(exData);
                ex = ex.InnerException;
            }

            if (ex != null)
            {
                data.ExceptionsChain.Add(new VSIExceptionData.Types.Exception
                {
                    ExceptionType = GetProto(typeof(ChainTooLongException))
                });
            }
        }

        List<VSIExceptionData.Types.Exception.Types.StackTraceFrame> GetStackTraceFrames(
            Exception ex)
        {
            var frames = new List<VSIExceptionData.Types.Exception.Types.StackTraceFrame>();
            var stackTrace = new StackTrace(ex, true);

            for (int curIndex = 0;
                curIndex < stackTrace.GetFrames()?.Length && curIndex < _maxStackTraceFrames;
                curIndex++)
            {
                var curFrame = stackTrace.GetFrame(curIndex);
                var curTransformedFrame =
                    new VSIExceptionData.Types.Exception.Types.StackTraceFrame();

                curTransformedFrame.AllowedNamespace =
                    IsMethodInAllowedNamespace(curFrame.GetMethod());

                if (curTransformedFrame.AllowedNamespace.Value)
                {
                    curTransformedFrame.Method = GetProto(curFrame.GetMethod());
                    curTransformedFrame.Filename = Path.GetFileName(curFrame.GetFileName());
                    curTransformedFrame.LineNumber = (uint?) curFrame.GetFileLineNumber();
                }

                frames.Add(curTransformedFrame);
            }

            return frames;
        }

        bool IsMethodInAllowedNamespace(MethodBase method)
        {
            string methodNamespace = method?.DeclaringType?.Namespace;

            if (string.IsNullOrEmpty(methodNamespace))
            {
                return false;
            }

            foreach (string curAllowedNamespace in _namespaceAllowList)
            {
                if (methodNamespace.StartsWith(curAllowedNamespace + ".") ||
                    methodNamespace == curAllowedNamespace)
                {
                    return true;
                }
            }

            return false;
        }

        public static VSIMethodInfo GetProto(MethodBase methodInfo) =>
            new VSIMethodInfo
            {
                NamespaceName = methodInfo.DeclaringType.Namespace ?? "",
                ClassName = methodInfo.DeclaringType.Name ?? "",
                MethodName = methodInfo.Name ?? ""
            };

        public static VSITypeInfo GetProto(Type type) =>
            new VSITypeInfo
            {
                NamespaceName = type.Namespace,
                ClassName = type.Name
            };

        public class ChainTooLongException : Exception
        {
        }
    }
}
