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
// limitations under the License.using Grpc.Core;

using Grpc.Core;
using Microsoft.Toolkit.HighPerformance.Buffers;
using System;
using System.Buffers;

namespace DebuggerGrpc
{
    public class SimpleSerializationContext : SerializationContext, IDisposable
    {
        ArrayPoolBufferWriter<byte> buffer_;

        public override void Complete(byte[] payload)
        {
            throw new NotImplementedException(
                $"The method '{nameof(Complete)}' should not be called. " +
                "If you are seeing this error, chances are you need to " +
                $"rebuild the '{nameof(DebuggerGrpc)}' project to regenerate proto classes.");
        }

        public byte[] GetPayload() => buffer_.WrittenSpan.ToArray();

        public override void Complete() { }

        public override IBufferWriter<byte> GetBufferWriter()
        {
            if (buffer_ == null)
            {
                buffer_ = new ArrayPoolBufferWriter<byte>();
            }
            return buffer_;
        }

        public override void SetPayloadLength(int payloadLength)
        {
            if (buffer_ != null)
            {
                throw new InvalidOperationException(
                    "SetPayloadLength is called after the buffer is already created!");
            }
            // Length might be -1 (i.e. "unknown"), don't pre-create a buffer in this case.
            if (payloadLength > 0)
            {
                buffer_ = new ArrayPoolBufferWriter<byte>(payloadLength);
            }
        }

        public void Dispose()
        {
            buffer_?.Dispose();
        }
    }

    public class SimpleDeserializationContext : DeserializationContext
    {
        public ReadOnlySequence<byte> payload_;

        public SimpleDeserializationContext(byte[] payload)
        {
            payload_ = new ReadOnlySequence<byte>(payload);
        }

        public override int PayloadLength => (int)payload_.Length;

        public override byte[] PayloadAsNewBuffer()
        {
            var buffer = new byte[payload_.Length];
            PayloadAsReadOnlySequence().CopyTo(buffer);
            return buffer;
        }

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        {
            return payload_;
        }
    }
}
