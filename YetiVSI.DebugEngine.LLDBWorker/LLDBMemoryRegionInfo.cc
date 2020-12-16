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

#include "LLDBMemoryRegionInfo.h"

namespace YetiVSI {
namespace DebugEngine {

LLDBMemoryRegionInfo::LLDBMemoryRegionInfo(lldb::SBMemoryRegionInfo memoryRegionInfo) {
  memoryRegionInfo_ = MakeUniquePtr<lldb::SBMemoryRegionInfo>(memoryRegionInfo);
}

uint64_t LLDBMemoryRegionInfo::GetRegionEnd() {
  return memoryRegionInfo_->GetRegionEnd();
}

bool LLDBMemoryRegionInfo::IsMapped() {
  return memoryRegionInfo_->IsMapped();
}

}  // namespace DebugEngine
}  // namespace YetiVSI