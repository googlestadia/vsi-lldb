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

#include "LLDBFileSpecFactory.h"

#include <msclr\marshal_cppstd.h>

#include "lldb/API/SBFileSpec.h"

#include "LLDBFileSpec.h"

namespace YetiVSI {
namespace DebugEngine {

SbFileSpec ^ LLDBFileSpecFactory::Create(System::String ^ directory,
  System::String ^ filename) {
  return gcnew LLDBFileSpec(lldb::SBFileSpec(
    msclr::interop::marshal_as<std::string>(
      System::IO::Path::Combine(directory, filename)).c_str()));
}
}  // namespace DebugEngine
}  // namespace YetiVSI
