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

namespace DebuggerApi
{
    // Naming is intentionally camel case to match proto generated code and support
    // EnumUtil::ConvertTo<T>().
    public enum ReturnStatus
    {
        Invalid,
        SuccessFinishNoResult,
        SuccessFinishResult,
        SuccessContinuingNoResult,
        SuccessContinuingResult,
        Started,
        Failed,
        Quit
    }

    // Display format definitions
    public enum ValueFormat
    {
        Default = 0,
        Invalid,
        Boolean,
        Binary,
        Bytes,
        BytesWithASCII,
        Char,
        CharPrintable, // Only printable characters, space if not printable
        Complex,       // Floating point complex type
        ComplexFloat,
        CString, // NULL terminated C strings
        Decimal,
        Enum,
        Hex,
        HexUppercase,
        Float,
        Octal,
        OSType, // OS character codes encoded into an integer 'PICT' 'text'
                // etc...
        Unicode16,
        Unicode32,
        Unsigned,
        Pointer,
        VectorOfChar,
        VectorOfSInt8,
        VectorOfUInt8,
        VectorOfSInt16,
        VectorOfUInt16,
        VectorOfSInt32,
        VectorOfUInt32,
        VectorOfSInt64,
        VectorOfUInt64,
        VectorOfFloat16,
        VectorOfFloat32,
        VectorOfFloat64,
        VectorOfUInt128,
        ComplexInteger, // Integer complex type
        CharArray,      // Print characters with no single quotes, used for
                        // character arrays that can contain non printable
                        // characters
        AddressInfo, // Describe what an address points to (func + offset with
                     // file/line, symbol + offset, data, etc)
        HexFloat,    // ISO C99 hex float string
        Instruction, // Disassemble an opcode
        Void,        // Do not print this
    }

    // Module section types
    public enum SectionType
    {
        Invalid,
        Code,
        Container, // The section contains child sections
        Data,
        DataCString,         // Inlined C string data
        DataCStringPointers, // Pointers to C string data
        DataSymbolAddress,   // Address of a symbol in the symbol table
        Data4,
        Data8,
        Data16,
        DataPointers,
        Debug,
        ZeroFill,
        DataObjCMessageRefs, // Pointer to function pointer + selector
        DataObjCCFStrings, // Objective-C const CFString/NSString objects
        DWARFDebugAbbrev,
        DWARFDebugAddr,
        DWARFDebugAranges,
        DWARFDebugCuIndex,
        DWARFDebugFrame,
        DWARFDebugInfo,
        DWARFDebugLine,
        DWARFDebugLoc,
        DWARFDebugMacInfo,
        DWARFDebugMacro,
        DWARFDebugPubNames,
        DWARFDebugPubTypes,
        DWARFDebugRanges,
        DWARFDebugStr,
        DWARFDebugStrOffsets,
        DWARFAppleNames,
        DWARFAppleTypes,
        DWARFAppleNamespaces,
        DWARFAppleObjC,
        ELFSymbolTable,       // Elf SHT_SYMTAB section
        ELFDynamicSymbols,    // Elf SHT_DYNSYM section
        ELFRelocationEntries, // Elf SHT_REL or SHT_REL section
        ELFDynamicLinkInfo,   // Elf SHT_DYNAMIC section
        EHFrame,
        ARMexidx,
        ARMextab,
        CompactUnwind, // compact unwind section in Mach-O,
                       // __TEXT,__unwind_info
        GoSymtab,
        AbsoluteAddress, // Dummy section for symbols with absolute
                         // address
        Other
    }

    // LLDB defines and constants
    public static class DebuggerConstants
    {
        // Value representing an invalid address in memory
        public const ulong INVALID_ADDRESS = ulong.MaxValue;
    }
}

