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

#include "LLDBValue.h"

#include <msclr\marshal_cppstd.h>

#include "LLDBError.h"
#include "LLDBExpressionOptions.h"
#include "LLDBType.h"
#include "ValueTypeUtil.h"

#include "lldb/API/SBError.h"
#include "lldb/API/SBProcess.h"
#include "lldb/API/SBStream.h"
#include "lldb/API/SBTarget.h"
#include "lldb/API/SBValue.h"


namespace YetiVSI {
namespace DebugEngine {

namespace {

lldb::Format Convert(LldbApi::ValueFormat format) {
  switch (format) {
    case LldbApi::ValueFormat::Default:
      return lldb::eFormatDefault;
    case LldbApi::ValueFormat::Invalid:
      return lldb::eFormatInvalid;
    case LldbApi::ValueFormat::Boolean:
      return lldb::eFormatBoolean;
    case LldbApi::ValueFormat::Binary:
      return lldb::eFormatBinary;
    case LldbApi::ValueFormat::Bytes:
      return lldb::eFormatBytes;
    case LldbApi::ValueFormat::BytesWithASCII:
      return lldb::eFormatBytesWithASCII;
    case LldbApi::ValueFormat::Char:
      return lldb::eFormatChar;
    case LldbApi::ValueFormat::CharPrintable:
      return lldb::eFormatCharPrintable;
    case LldbApi::ValueFormat::Complex:
      return lldb::eFormatComplex;
    case LldbApi::ValueFormat::ComplexFloat:
      return lldb::eFormatComplexFloat;
    case LldbApi::ValueFormat::CString:
      return lldb::eFormatCString;
    case LldbApi::ValueFormat::Decimal:
      return lldb::eFormatDecimal;
    case LldbApi::ValueFormat::Enum:
      return lldb::eFormatEnum;
    case LldbApi::ValueFormat::Hex:
      return lldb::eFormatHex;
    case LldbApi::ValueFormat::HexUppercase:
      return lldb::eFormatHexUppercase;
    case LldbApi::ValueFormat::Float:
      return lldb::eFormatFloat;
    case LldbApi::ValueFormat::Octal:
      return lldb::eFormatOctal;
    case LldbApi::ValueFormat::OSType:
      return lldb::eFormatOSType;
    case LldbApi::ValueFormat::Unicode16:
      return lldb::eFormatUnicode16;
    case LldbApi::ValueFormat::Unicode32:
      return lldb::eFormatUnicode32;
    case LldbApi::ValueFormat::Unsigned:
      return lldb::eFormatUnsigned;
    case LldbApi::ValueFormat::Pointer:
      return lldb::eFormatPointer;
    case LldbApi::ValueFormat::VectorOfChar:
      return lldb::eFormatVectorOfChar;
    case LldbApi::ValueFormat::VectorOfSInt8:
      return lldb::eFormatVectorOfSInt8;
    case LldbApi::ValueFormat::VectorOfUInt8:
      return lldb::eFormatVectorOfUInt8;
    case LldbApi::ValueFormat::VectorOfSInt16:
      return lldb::eFormatVectorOfSInt16;
    case LldbApi::ValueFormat::VectorOfUInt16:
      return lldb::eFormatVectorOfUInt16;
    case LldbApi::ValueFormat::VectorOfSInt32:
      return lldb::eFormatVectorOfSInt32;
    case LldbApi::ValueFormat::VectorOfUInt32:
      return lldb::eFormatVectorOfUInt32;
    case LldbApi::ValueFormat::VectorOfSInt64:
      return lldb::eFormatVectorOfSInt64;
    case LldbApi::ValueFormat::VectorOfUInt64:
      return lldb::eFormatVectorOfUInt64;
    case LldbApi::ValueFormat::VectorOfFloat16:
      return lldb::eFormatVectorOfFloat16;
    case LldbApi::ValueFormat::VectorOfFloat32:
      return lldb::eFormatVectorOfFloat32;
    case LldbApi::ValueFormat::VectorOfFloat64:
      return lldb::eFormatVectorOfFloat64;
    case LldbApi::ValueFormat::VectorOfUInt128:
      return lldb::eFormatVectorOfUInt128;
    case LldbApi::ValueFormat::ComplexInteger:
      return lldb::eFormatComplexInteger;
    case LldbApi::ValueFormat::CharArray:
      return lldb::eFormatCharArray;
    case LldbApi::ValueFormat::AddressInfo:
      return lldb::eFormatAddressInfo;
    case LldbApi::ValueFormat::HexFloat:
      return lldb::eFormatHexFloat;
    case LldbApi::ValueFormat::Instruction:
      return lldb::eFormatInstruction;
    case LldbApi::ValueFormat::Void:
      return lldb::eFormatVoid;
    default:
      return lldb::eFormatDefault;
  }
}

LldbApi::ValueFormat Convert(lldb::Format format) {
  switch (format) {
    case lldb::eFormatDefault:  // == eFormatInvalid
      return LldbApi::ValueFormat::Default;
    case lldb::eFormatBoolean:
      return LldbApi::ValueFormat::Boolean;
    case lldb::eFormatBinary:
      return LldbApi::ValueFormat::Binary;
    case lldb::eFormatBytes:
      return LldbApi::ValueFormat::Bytes;
    case lldb::eFormatBytesWithASCII:
      return LldbApi::ValueFormat::BytesWithASCII;
    case lldb::eFormatChar:
      return LldbApi::ValueFormat::Char;
    case lldb::eFormatCharPrintable:
      return LldbApi::ValueFormat::CharPrintable;
    case lldb::eFormatComplex:  // == eFormatComplexFloat
      return LldbApi::ValueFormat::Complex;
    case lldb::eFormatCString:
      return LldbApi::ValueFormat::CString;
    case lldb::eFormatDecimal:
      return LldbApi::ValueFormat::Decimal;
    case lldb::eFormatEnum:
      return LldbApi::ValueFormat::Enum;
    case lldb::eFormatHex:
      return LldbApi::ValueFormat::Hex;
    case lldb::eFormatHexUppercase:
      return LldbApi::ValueFormat::HexUppercase;
    case lldb::eFormatFloat:
      return LldbApi::ValueFormat::Float;
    case lldb::eFormatOctal:
      return LldbApi::ValueFormat::Octal;
    case lldb::eFormatOSType:
      return LldbApi::ValueFormat::OSType;
    case lldb::eFormatUnicode16:
      return LldbApi::ValueFormat::Unicode16;
    case lldb::eFormatUnicode32:
      return LldbApi::ValueFormat::Unicode32;
    case lldb::eFormatUnsigned:
      return LldbApi::ValueFormat::Unsigned;
    case lldb::eFormatPointer:
      return LldbApi::ValueFormat::Pointer;
    case lldb::eFormatVectorOfChar:
      return LldbApi::ValueFormat::VectorOfChar;
    case lldb::eFormatVectorOfSInt8:
      return LldbApi::ValueFormat::VectorOfSInt8;
    case lldb::eFormatVectorOfUInt8:
      return LldbApi::ValueFormat::VectorOfUInt8;
    case lldb::eFormatVectorOfSInt16:
      return LldbApi::ValueFormat::VectorOfSInt16;
    case lldb::eFormatVectorOfUInt16:
      return LldbApi::ValueFormat::VectorOfUInt16;
    case lldb::eFormatVectorOfSInt32:
      return LldbApi::ValueFormat::VectorOfSInt32;
    case lldb::eFormatVectorOfUInt32:
      return LldbApi::ValueFormat::VectorOfUInt32;
    case lldb::eFormatVectorOfSInt64:
      return LldbApi::ValueFormat::VectorOfSInt64;
    case lldb::eFormatVectorOfUInt64:
      return LldbApi::ValueFormat::VectorOfUInt64;
    case lldb::eFormatVectorOfFloat16:
      return LldbApi::ValueFormat::VectorOfFloat16;
    case lldb::eFormatVectorOfFloat32:
      return LldbApi::ValueFormat::VectorOfFloat32;
    case lldb::eFormatVectorOfFloat64:
      return LldbApi::ValueFormat::VectorOfFloat64;
    case lldb::eFormatVectorOfUInt128:
      return LldbApi::ValueFormat::VectorOfUInt128;
    case lldb::eFormatComplexInteger:
      return LldbApi::ValueFormat::ComplexInteger;
    case lldb::eFormatCharArray:
      return LldbApi::ValueFormat::CharArray;
    case lldb::eFormatAddressInfo:
      return LldbApi::ValueFormat::AddressInfo;
    case lldb::eFormatHexFloat:
      return LldbApi::ValueFormat::HexFloat;
    case lldb::eFormatInstruction:
      return LldbApi::ValueFormat::Instruction;
    case lldb::eFormatVoid:
      return LldbApi::ValueFormat::Void;
    default:
      return LldbApi::ValueFormat::Default;
  }
}

// Returns buff+i for the first i for which buff[i]==0 or i==numElements,
// whatever comes first.
template <typename T>
const T* FindNullTerminator(const T* buff, size_t numElements) {
  const T* ptr = buff;
  const T* end = buff + numElements;
  while (ptr < end && *ptr != 0) {
    ++ptr;
  }
  return ptr;
}

// Copies the data at |data| of size |dataSize| to a managed array.
array<unsigned char> ^ ToArray(const void* data, size_t dataSize) {
  auto managedData = gcnew array<unsigned char>(static_cast<int>(dataSize));
  if (dataSize > 0) {
    pin_ptr<unsigned char> pinnedBytes = &managedData[0];
    memcpy(pinnedBytes, data, dataSize);
  }
  return managedData;
}

}  // namespace

LLDBValue::LLDBValue(lldb::SBValue value) {
  value_ = MakeUniquePtr<lldb::SBValue>(value);
}

LLDBValue::LLDBValue(lldb::SBValue value, lldb::SBError error)
    : LLDBValue(value) {
  error_ = MakeUniquePtr<lldb::SBError>(error);
}

System::String ^ LLDBValue::GetName() {
  return gcnew System::String(value_->GetName());
}

System::String ^ LLDBValue::GetValue() {
  return gcnew System::String(value_->GetValue());
}

LldbApi::ValueFormat LLDBValue::GetFormat() {
  auto lldb_format = value_->GetFormat();
  return Convert(lldb_format);
}

void LLDBValue::SetFormat(LldbApi::ValueFormat format) {
  auto lldb_format = Convert(format);
  value_->SetFormat(lldb_format);
}

SbType ^ LLDBValue::GetTypeInfo() {
  auto typeInfo = value_->GetType();
  if (typeInfo.IsValid()) {
    return gcnew LLDBType(typeInfo);
  }
  return nullptr;
}

System::String ^ LLDBValue::GetTypeName() {
  return gcnew System::String(value_->GetTypeName());
}

System::String ^ LLDBValue::GetSummary() {
  return gcnew System::String(value_->GetSummary());
}

ValueType LLDBValue::GetValueType() {
  return ToLldbApiValueType(value_->GetValueType());
}

SbError ^ LLDBValue::GetError() {
  // Unfortunately there is no API to put an error inside lldb::SBValue, so we
  // have to store lldb::SBError along with the value.
  lldb::SBError error =
      error_ != nullptr ? *(*error_).Get() : value_->GetError();

  // Error returned by lldb::SBValue is always "valid".
  return gcnew LLDBError(error);
}

uint32_t LLDBValue::GetNumChildren() { return value_->GetNumChildren(); }

SbValue ^ LLDBValue::GetChildAtIndex(uint32_t index) {
  auto value = value_->GetChildAtIndex(index);
  if (value.IsValid()) {
    return gcnew LLDBValue(value);
  }
  return nullptr;
}

System::Collections::Generic::List<SbValue ^> ^
    LLDBValue::GetChildren(uint32_t indexOffset, uint32_t count) {
  auto values = gcnew System::Collections::Generic::List<SbValue ^>(count);
  for (uint32_t index = indexOffset; index < indexOffset + count; index++) {
    values->Add(GetChildAtIndex(index));
  }
  return values;
}

SbValue ^ LLDBValue::CreateValueFromExpression(System::String ^ name,
                                               System::String ^ expression,
                                               SbExpressionOptions ^ options) {
  auto lldbExpressionOptions = safe_cast<LLDBExpressionOptions ^>(options);
  auto expressionValue = value_->CreateValueFromExpression(
      msclr::interop::marshal_as<std::string>(name).c_str(),
      msclr::interop::marshal_as<std::string>(expression).c_str(),
      lldbExpressionOptions->GetNativeObject());
  if (expressionValue.IsValid()) {
    return gcnew LLDBValue(expressionValue);
  }
  return nullptr;
}

SbValue ^ LLDBValue::CreateValueFromAddress(System::String ^ name,
                                            uint64_t address, SbType ^ type) {
  auto valueType = safe_cast<LLDBType ^>(type);
  auto expressionValue = value_->CreateValueFromAddress(
      msclr::interop::marshal_as<std::string>(name).c_str(), address,
      valueType->GetNativeObject());
  if (expressionValue.IsValid()) {
    return gcnew LLDBValue(expressionValue);
  }
  return nullptr;
}

SbValue ^ LLDBValue::EvaluateExpression(System::String ^ expression,
                                        SbExpressionOptions ^ options) {
  auto lldbExpressionOptions = safe_cast<LLDBExpressionOptions ^>(options);
  auto expressionValue = value_->EvaluateExpression(
      msclr::interop::marshal_as<std::string>(expression).c_str(),
      lldbExpressionOptions->GetNativeObject());
  if (expressionValue.IsValid()) {
    return gcnew LLDBValue(expressionValue);
  }
  return nullptr;
}

uint64_t LLDBValue::GetValueAsUnsigned() {
  return value_->GetValueAsUnsigned();
}

SbValue ^ LLDBValue::Clone() {
  lldb::SBData data = value_->GetData();
  lldb::SBError ignore;
  auto rawData = std::make_unique<uint8_t[]>(data.GetByteSize());
  data.ReadRawData(ignore, 0, rawData.get(), data.GetByteSize());

  lldb::SBTarget target = value_->GetTarget();
  lldb::SBType type = value_->GetType();

  // Create value from bytes.
  lldb::SBData cloneData;
  cloneData.SetData(
      ignore, rawData.get(), type.GetByteSize(), target.GetByteOrder(),
      static_cast<uint8_t>(target.GetAddressByteSize()));
  lldb::SBValue cloneValue =
      target.CreateValueFromData(value_->GetName(), cloneData, type).GetStaticValue();

  return gcnew LLDBValue(cloneValue);
}

SbValue ^ LLDBValue::Dereference() {
  lldb::SBValue dereferenceValue = value_->Dereference();
  if (dereferenceValue.IsValid()) {
    return gcnew LLDBValue(dereferenceValue);
  }
  return nullptr;
}

SbValue ^ LLDBValue::GetChildMemberWithName(System::String ^ name) {
  auto childValue = value_->GetChildMemberWithName(
      msclr::interop::marshal_as<std::string>(name).c_str());
  if (childValue.IsValid()) {
    return gcnew LLDBValue(childValue);
  }
  return nullptr;
}

SbValue ^ LLDBValue::AddressOf() {
  auto address = value_->AddressOf();
  if (address.IsValid()) {
    return gcnew LLDBValue(address);
  }
  return nullptr;
}

bool LLDBValue::TypeIsPointerType() { return value_->TypeIsPointerType(); }

SbValue ^
    LLDBValue::GetValueForExpressionPath(System::String ^ expressionPath) {
  auto childValue = value_->GetValueForExpressionPath(
      msclr::interop::marshal_as<std::string>(expressionPath).c_str());
  if (childValue.IsValid()) {
    return gcnew LLDBValue(childValue);
  }
  return nullptr;
}

bool LLDBValue::GetExpressionPath(
    [System::Runtime::InteropServices::Out] System::String ^ % path) {
  lldb::SBStream stream;
  bool returnValue = value_->GetExpressionPath(stream);
  path = returnValue ? gcnew System::String(stream.GetData()) : nullptr;
  return returnValue;
}

uint64_t LLDBValue::GetByteSize() { return value_->GetByteSize(); }

array<unsigned char> ^
    LLDBValue::GetPointeeAsByteString(
        uint32_t charSize, uint32_t maxStringSize,
        [System::Runtime::InteropServices::Out] System::String ^ % error) {
  error = nullptr;
  if (charSize != 1 && charSize != 2 && charSize != 4) {
    // This is a bug in the calling code and should be reported in logs.
    throw gcnew System::ArgumentException(
        System::String::Format("Invalid charSize {0}", charSize));
  }

  lldb::SBProcess process = value_->GetProcess();
  if (!process.IsValid()) {
    // This should never happen(TM).
    error = "<unknown error>";
    return nullptr;
  }

  // Figure out the address where the string starts.
  // This has to be done differently for pointers and arrays.
  uint64_t address = 0;
  if (value_->GetType().IsPointerType()) {
    address = value_->GetValueAsUnsigned();
  } else if (value_->GetType().IsArrayType()) {
    if (value_->GetNumChildren() == 0) {
      // A char array of size 0 still has byte size 1, hence this special case.
      return gcnew array<unsigned char>(0);
    }

    // Arrays don't necessarily have a null terminator, so limit the size.
    maxStringSize =
        static_cast<uint32_t>(min(maxStringSize, value_->GetByteSize()));
    address = value_->GetLoadAddress();
  } else {
    error = "<type must be pointer or array>";
    return nullptr;
  }
  if (address == 0) {
    error = "<NULL>";
    return nullptr;
  }
  if (address == LLDB_INVALID_ADDRESS) {
    // Could potentially happen from value_->GetLoadAddress() if
    // !value_.IsValid(), but not sure if that could ever be the case.
    error = "<invalid>";
    return nullptr;
  }

  // Start reading a small number of bytes to read and successively increase the
  // number of byte. This keeps the chance of a memory fetch from LLDB server
  // low for short strings, while not trashing performance for large ones.
  size_t bytesToRead = 64;
  const size_t MAX_BYTES_TO_READ = 1024 * 64;

  std::vector<uint8_t> data;
  size_t totalBytesToRead = maxStringSize;
  while (totalBytesToRead > 0) {
    // Determine number of bytes to read and resize data.
    bytesToRead = min(bytesToRead, totalBytesToRead);
    size_t prevDataSize = data.size();
    data.resize(prevDataSize + bytesToRead);
    uint8_t* readBuff = &data[prevDataSize];

    lldb::SBError error;
    size_t bytesRead =
        process.ReadMemory(address, readBuff, bytesToRead, error);

    address += bytesRead;
    totalBytesToRead -= bytesRead;

    const void* endPos;
    if (charSize == 1) {
      endPos = FindNullTerminator(readBuff, bytesRead);
    } else if (charSize == 2) {
      endPos = FindNullTerminator(reinterpret_cast<uint16_t*>(readBuff),
                                  bytesRead / 2);
    } else {  // charSize == 4
      endPos = FindNullTerminator(reinterpret_cast<uint32_t*>(readBuff),
                                  bytesRead / 4);
    }

    const size_t bytesUntilNull =
        reinterpret_cast<const uint8_t*>(endPos) - readBuff;
    if (bytesUntilNull < bytesToRead) {
      // Usually means null terminator was found. Might also happen when
      // ReadMemory() partially failed and bytesRead < bytesToRead, in which
      // case we want to stop as well.
      data.resize(prevDataSize + bytesUntilNull);
      break;
    }

    // Read a bit more next time.
    bytesToRead = min(bytesToRead * 2, MAX_BYTES_TO_READ);
  }

  return ToArray(data.data(), data.size());
}

lldb::SBValue LLDBValue::GetNativeObject() { return *(*value_).Get(); }

}  // namespace DebugEngine
}  // namespace YetiVSI
