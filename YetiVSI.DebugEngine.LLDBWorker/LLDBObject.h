/*
 * Copyright 2020 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#pragma once

namespace YetiVSI {
namespace DebugEngine {

using namespace LldbApi;

// This class wraps LLDB API objects in order to access the protected underlying object pointer.
// Can be used to retrieve an ID.
template <typename T>
class LLDBObject : public T {
 public:
  LLDBObject(T& obj) : T(obj) {}

  int64_t GetSPAddress() {
    return reinterpret_cast<int64_t>(GetSP().get());
  }
};

// Accepts an LLDB object and obtains the underlying shared object address.  Can be used as an ID.
template<typename T>
int64_t GetSPAddress(T& obj) {
  return LLDBObject<T>(obj).GetSPAddress();
}

}  // namespace DebugEngine
}  // namespace YetiVSI