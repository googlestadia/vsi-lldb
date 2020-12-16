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

﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Runtime.InteropServices;
using TestsCommon.TestSupport;

namespace Google.VisualStudioFake.Internal.Interop
{
    public class FileLineBreakpointRequest : IBreakpointRequest
    {
        bool disposed = false;
        readonly IntPtr docPosition;

        public FileLineBreakpointRequest(string fileName, int lineNumber)
        {
            docPosition = Marshal.GetIUnknownForObject(new DocumentPosition(fileName, lineNumber));
        }

        ~FileLineBreakpointRequest()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            Marshal.Release(docPosition);
        }

        public int GetLocationType(enum_BP_LOCATION_TYPE[] locationType)
        {
            if (locationType.Length != 1)
            {
                throw new ArgumentException($"Expected {nameof(locationType)} to have " +
                    $"length = 1; was {locationType.Length}.");
            }
            locationType[0] = enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE;
            return VSConstants.S_OK;
        }

        public int GetRequestInfo(enum_BPREQI_FIELDS fields, BP_REQUEST_INFO[] requestInfo)
        {
            if (requestInfo.Length != 1)
            {
                throw new ArgumentException($"Expected {nameof(requestInfo)} to have " +
                    $"length = 1; was {requestInfo.Length}.");
            }

            var info = new BP_REQUEST_INFO()
            {
                bpLocation = new BP_LOCATION()
                {
                    bpLocationType = (int)enum_BP_LOCATION_TYPE.BPLT_CODE_FILE_LINE,
                    unionmember2 = docPosition
                },
                bpPassCount = new BP_PASSCOUNT()
                {
                    dwPassCount = 1
                }
            };

            requestInfo[0] = info;
            return VSConstants.S_OK;
        }

        class DocumentPosition : IDebugDocumentPosition2
        {
            readonly string fileName;
            readonly int lineNumber;

            public DocumentPosition(string fileName, int lineNumber)
            {
                this.fileName = fileName;
                this.lineNumber = lineNumber;
            }

            public int GetFileName(out string pbstrFileName)
            {
                pbstrFileName = fileName;
                return VSConstants.S_OK;
            }

            public int GetRange(TEXT_POSITION[] pBegPosition, TEXT_POSITION[] pEndPosition)
            {
                if (pBegPosition.Length != 1)
                {
                    throw new ArgumentException("Expected start position array to have exactly " +
                        $"one entry; it had {pBegPosition.Length}");
                }

                pBegPosition[0].dwLine = (uint)lineNumber - 1;
                return VSConstants.S_OK;
            }

            #region Not Implemented

            public int GetDocument(out IDebugDocument2 ppDoc)
            {
                throw new NotImplementedTestDoubleException();
            }

            public int IsPositionInDocument(IDebugDocument2 pDoc)
            {
                throw new NotImplementedTestDoubleException();
            }

            #endregion
        }
    }
}
