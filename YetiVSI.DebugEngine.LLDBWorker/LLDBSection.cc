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

#include "LLDBSection.h"

#include "LLDBTarget.h"

namespace YetiVSI {
namespace DebugEngine {

namespace {

LldbApi::SectionType Convert(lldb::SectionType sectionType) {
  switch (sectionType) {
    case lldb::eSectionTypeInvalid:
      return LldbApi::SectionType::Invalid;
    case lldb::eSectionTypeCode:
      return LldbApi::SectionType::Code;
    case lldb::eSectionTypeContainer:
      return LldbApi::SectionType::Container;
    case lldb::eSectionTypeData:
      return LldbApi::SectionType::Data;
    case lldb::eSectionTypeDataCString:
      return LldbApi::SectionType::DataCString;
    case lldb::eSectionTypeDataCStringPointers:
      return LldbApi::SectionType::DataCStringPointers;
    case lldb::eSectionTypeDataSymbolAddress:
      return LldbApi::SectionType::DataSymbolAddress;
    case lldb::eSectionTypeData4:
      return LldbApi::SectionType::Data4;
    case lldb::eSectionTypeData8:
      return LldbApi::SectionType::Data8;
    case lldb::eSectionTypeData16:
      return LldbApi::SectionType::Data16;
    case lldb::eSectionTypeDataPointers:
      return LldbApi::SectionType::DataPointers;
    case lldb::eSectionTypeDebug:
      return LldbApi::SectionType::Debug;
    case lldb::eSectionTypeZeroFill:
      return LldbApi::SectionType::ZeroFill;
    case lldb::eSectionTypeDataObjCMessageRefs:
      return LldbApi::SectionType::DataObjCMessageRefs;
    case lldb::eSectionTypeDataObjCCFStrings:
      return LldbApi::SectionType::DataObjCCFStrings;
    case lldb::eSectionTypeDWARFDebugAbbrev:
      return LldbApi::SectionType::DWARFDebugAbbrev;
    case lldb::eSectionTypeDWARFDebugAddr:
      return LldbApi::SectionType::DWARFDebugAddr;
    case lldb::eSectionTypeDWARFDebugAranges:
      return LldbApi::SectionType::DWARFDebugAranges;
    case lldb::eSectionTypeDWARFDebugCuIndex:
      return LldbApi::SectionType::DWARFDebugCuIndex;
    case lldb::eSectionTypeDWARFDebugFrame:
      return LldbApi::SectionType::DWARFDebugFrame;
    case lldb::eSectionTypeDWARFDebugInfo:
      return LldbApi::SectionType::DWARFDebugInfo;
    case lldb::eSectionTypeDWARFDebugLine:
      return LldbApi::SectionType::DWARFDebugLine;
    case lldb::eSectionTypeDWARFDebugLoc:
      return LldbApi::SectionType::DWARFDebugLoc;
    case lldb::eSectionTypeDWARFDebugMacInfo:
      return LldbApi::SectionType::DWARFDebugMacInfo;
    case lldb::eSectionTypeDWARFDebugMacro:
      return LldbApi::SectionType::DWARFDebugMacro;
    case lldb::eSectionTypeDWARFDebugPubNames:
      return LldbApi::SectionType::DWARFDebugPubNames;
    case lldb::eSectionTypeDWARFDebugPubTypes:
      return LldbApi::SectionType::DWARFDebugPubTypes;
    case lldb::eSectionTypeDWARFDebugRanges:
      return LldbApi::SectionType::DWARFDebugRanges;
    case lldb::eSectionTypeDWARFDebugStr:
      return LldbApi::SectionType::DWARFDebugStr;
    case lldb::eSectionTypeDWARFDebugStrOffsets:
      return LldbApi::SectionType::DWARFDebugStrOffsets;
    case lldb::eSectionTypeDWARFAppleNames:
      return LldbApi::SectionType::DWARFAppleNames;
    case lldb::eSectionTypeDWARFAppleTypes:
      return LldbApi::SectionType::DWARFAppleTypes;
    case lldb::eSectionTypeDWARFAppleNamespaces:
      return LldbApi::SectionType::DWARFAppleNamespaces;
    case lldb::eSectionTypeDWARFAppleObjC:
      return LldbApi::SectionType::DWARFAppleObjC;
    case lldb::eSectionTypeELFSymbolTable:
      return LldbApi::SectionType::ELFSymbolTable;
    case lldb::eSectionTypeELFDynamicSymbols:
      return LldbApi::SectionType::ELFDynamicSymbols;
    case lldb::eSectionTypeELFRelocationEntries:
      return LldbApi::SectionType::ELFRelocationEntries;
    case lldb::eSectionTypeELFDynamicLinkInfo:
      return LldbApi::SectionType::ELFDynamicLinkInfo;
    case lldb::eSectionTypeEHFrame:
      return LldbApi::SectionType::EHFrame;
    case lldb::eSectionTypeARMexidx:
      return LldbApi::SectionType::ARMexidx;
    case lldb::eSectionTypeARMextab:
      return LldbApi::SectionType::ARMextab;
    case lldb::eSectionTypeCompactUnwind:
      return LldbApi::SectionType::CompactUnwind;
    case lldb::eSectionTypeGoSymtab:
      return LldbApi::SectionType::GoSymtab;
    case lldb::eSectionTypeAbsoluteAddress:
      return LldbApi::SectionType::AbsoluteAddress;
    default:
      return LldbApi::SectionType::Other;
  }
}

}  // namespace

LLDBSection::LLDBSection(lldb::SBSection section) {
  section_ = MakeUniquePtr<lldb::SBSection>(section);
}

LldbApi::SectionType YetiVSI::DebugEngine::LLDBSection::GetSectionType() {
  return Convert(section_->GetSectionType());
}

uint64_t YetiVSI::DebugEngine::LLDBSection::GetLoadAddress(LldbApi::SbTarget ^
                                                           target) {
  LLDBTarget ^ lldbTarget = safe_cast<LLDBTarget ^>(target);
  return section_->GetLoadAddress(lldbTarget->GetNativeObject());
}

uint64_t YetiVSI::DebugEngine::LLDBSection::GetFileAddress() {
  return section_->GetFileAddress();
}

uint64_t YetiVSI::DebugEngine::LLDBSection::GetFileOffset() {
  return section_->GetFileOffset();
}

}  // namespace DebugEngine
}  // namespace YetiVSI
