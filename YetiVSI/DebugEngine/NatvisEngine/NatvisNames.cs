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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.Linq;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    public class TypeName
    {
        static readonly Regex IDENTIFIER_REGEX = new Regex("^[a-zA-Z$_][a-zA-Z$_0-9]*");

        // only decimal constants
        static readonly Regex NUMERIC_REGEX = new Regex("^[0-9]+");

        // helper patterns
        const string SIGN_PATTERN = @"(signed|unsigned)";
        static readonly string LONG_PATTERN = $@"(({SIGN_PATTERN}\s+)?(long\s+)?long)";

        // matches prefixes ending in '$' (e.g. void$Foo), these are checked for below
        static readonly Regex SIMPLE_TYPE_REGEX = new Regex(
            @"^unsigned\schar\s__attribute__\(\(ext_vector_type\(\d+\)\)\)|" + // ext vector
            @"^(" +
            @"(long\s+)?double|" + // double
            @"(long\s+)?float|" + // float
            @"char16_t|char32_t|wchar_t|" + // special chars
            @"u?int(8|16|32|64)_t|" + // special ints
            @"bool|void|" + // misc
            $@"(({SIGN_PATTERN}|{LONG_PATTERN})\s+)?int|" + // int
            $@"{LONG_PATTERN}|" + // long
            $@"({SIGN_PATTERN}\s+)?short(\s+int)?|" + // short
            $@"({SIGN_PATTERN}\s+)?char|" + // char
            $@"{SIGN_PATTERN}" + // signed/unsigned
            @")\b"
        );

        static readonly TypeName ANY_TYPE = new TypeName() {IsWildcard = true};

        public string FullyQualifiedName { get; private set; }
        public IReadOnlyList<TypeName> Qualifiers { get; private set; }

        public string BaseName { get; set; }

        // template arguements
        public IReadOnlyList<TypeName> Args { get; private set; }

        // parameter types
        public IReadOnlyList<TypeName> Parameters { get; private set; }

        public bool IsWildcard { get; private set; }
        public bool IsArray { get; private set; }
        public bool IsFunction { get; private set; }
        public int[] Dimensions { get; private set; }

        /// <summary>
        /// Return a parsed type name
        /// Acceptable name format:
        ///     typeName = ["const "] ([unqualifiedName "::"]* unqualifiedName | simpleTypeName)
        ///         ['*'] [Array] | functionDef
        ///     unqualifiedName = identifier | identifier '<' templateList '>'
        ///     templatelist = listElem  | listElem ',' templateList
        ///     listElem = typeName | numericConstant | '*'
        ///     Array = '[]' | '[' [numericConstant ',']* numericConstant ']'
        ///     functionDef = typeName (parameterList)
        ///
        /// </summary>
        /// <param name="fullyQualifiedName"></param>
        /// <returns></returns>
        public static TypeName Parse(string fullyQualifiedName)
        {
            if (string.IsNullOrEmpty(fullyQualifiedName))
            {
                return null;
            }

            string rest;
            var t = MatchTypeName(fullyQualifiedName.Trim(), out rest);
            if (!string.IsNullOrWhiteSpace(rest))
            {
                Trace.WriteLine($"ERROR: Natvis failed to parse typename: {fullyQualifiedName}");
                return null;
            }

            return t;
        }

        /// <summary>
        /// Match this typeName to a candidate typeName. This type support wildcard matching against
        /// the candidate
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool Match(TypeName t)
        {
            if (IsWildcard)
            {
                return true;
            }

            if (Qualifiers.Count != t.Qualifiers.Count)
            {
                return false;
            }

            if (BaseName != t.BaseName)
            {
                return false;
            }

            if (Qualifiers.Where((qualifier, i) => !qualifier.Match(t.Qualifiers[i])).Any())
            {
                return false;
            }

            // args must match one-for one,
            // or if last arg is a wildcard it will match any number of additional args
            if (Args.Count > t.Args.Count || (Args.Count == 0 && t.Args.Count > 0) ||
                (Args.Count < t.Args.Count && !Args[Args.Count - 1].IsWildcard))
            {
                return false;
            }

            if (Args.Where((arg, i) => !arg.Match(t.Args[i])).Any())
            {
                return false;
            }

            if (IsArray != t.IsArray)
            {
                return false;
            }

            if (!IsArray)
            {
                return true;
            }

            if (Dimensions.Length != t.Dimensions.Length)
            {
                return false;
            }

            return Dimensions.Where((dimension, i) => dimension != t.Dimensions[i])
                .All(dimension => dimension == -1);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name">Trimmed string containing a type name</param>
        /// <param name="rest">Trimmed remainder of string after name match</param>
        /// <returns></returns>
        static TypeName MatchTypeName(string name, out string rest)
        {
            var original = name;
            if (name.StartsWith("const ", StringComparison.Ordinal))
            {
                // TODO: we just ignore const
                name = name.Substring(6).Trim();
            }

            var t = MatchSimpleTypeName(name, out rest);
            if (t == null)
            {
                var qualifiers = new List<TypeName>();
                t = MatchUnqualifiedName(name, out rest);
                while (t != null && rest.Length > 2 &&
                       rest.StartsWith("::", StringComparison.Ordinal))
                {
                    // process qualifiers
                    qualifiers.Add(t);
                    t = MatchUnqualifiedName(rest.Substring(2).Trim(), out rest);
                }

                if (t == null)
                {
                    return null;
                }

                t.Qualifiers = qualifiers; // add qualifiers to the type
            }

            if (rest.StartsWith("const", StringComparison.Ordinal))
            {
                rest = rest.Substring(5).Trim();
            }

            while (rest.StartsWith("*", StringComparison.Ordinal) ||
                   rest.StartsWith("&", StringComparison.Ordinal))
            {
                t.BaseName += rest[0];
                rest = rest.Substring(1).Trim();
                if (rest.StartsWith("const", StringComparison.Ordinal))
                {
                    rest = rest.Substring(5).Trim();
                }
            }

            MatchArray(t, rest, out rest); // add array or pointer
            if (rest.StartsWith("(", StringComparison.Ordinal))
            {
                t.IsFunction = true;

                var parameters = new List<TypeName>();
                if (!MatchParameterList(rest.Substring(1).Trim(), out rest, parameters))
                {
                    return null;
                }

                t.Parameters = parameters;

                if (rest.Length > 0 && rest[0] == ')')
                {
                    rest = rest.Substring(1).Trim();
                }
                else
                {
                    return null;
                }
            }

            // complete the full name of the type
            t.FullyQualifiedName = original.Substring(0, original.Length - rest.Length);
            return t;
        }

        static TypeName MatchSimpleTypeName(string name, out string rest)
        {
            rest = string.Empty;
            var m = SIMPLE_TYPE_REGEX.Match(name);
            if (!m.Success) return null;
            // The simpleType regular expression will succeed for strings that look like
            // simple types, but are terminated by '$'.
            // Since the $ is a valid C++ identifier character we check it here to make
            // sure we haven't accidentally matched a prefix, e.g. int$Foo
            var r = name.Substring(m.Length);
            if (r.Length > 0 && r[0] == '$')
            {
                return null;
            }

            rest = r.Trim();
            return new TypeName(m.Value);
        }

        static TypeName MatchUnqualifiedName(string name, out string rest)
        {
            var basename = MatchIdentifier(name, out rest);
            if (string.IsNullOrEmpty(basename))
            {
                return null;
            }

            var t = new TypeName(basename);
            if (rest.Length <= 0 || rest[0] != '<')
            {
                return t;
            }

            var args = new List<TypeName>();
            if (!MatchTemplateList(rest.Substring(1).Trim(), out rest, args) ||
                rest.Length < 1 || rest[0] != '>')
            {
                return null;
            }

            t.Args = args;

            rest = rest.Substring(1).Trim();

            return t;
        }

        static void MatchArray(TypeName t, string name, out string rest)
        {
            if (name.StartsWith("[]", StringComparison.Ordinal))
            {
                t.SetArraySize(new int[] {-1});
                rest = name.Substring(2).Trim();
            }
            else if (name.StartsWith("[", StringComparison.Ordinal))
            {
                // TODO: handle multiple dimensions

                var num = MatchConstant(name.Substring(1).Trim(), out rest);
                if (rest.StartsWith("]", StringComparison.Ordinal))
                {
                    t.SetArraySize(new[] {int.Parse(num, CultureInfo.InvariantCulture)});
                }

                rest = rest.Substring(1).Trim();
            }
            else
            {
                rest = name;
            }
        }

        static string MatchIdentifier(string name, out string rest)
        {
            rest = string.Empty;
            var m = IDENTIFIER_REGEX.Match(name);
            if (m.Success)
            {
                rest = name.Substring(m.Length).Trim();
            }

            return m.Value;
        }

        static string MatchConstant(string name, out string rest)
        {
            rest = string.Empty;
            var m = NUMERIC_REGEX.Match(name);
            if (m.Success)
            {
                rest = name.Substring(m.Length).Trim();
            }

            return m.Value;
        }

        static bool MatchTemplateList(string templist, out string rest, ICollection<TypeName> args)
        {
            TypeName t;
            // no constants allowed in parameter lists
            var arg = MatchConstant(templist, out rest);
            if (!string.IsNullOrEmpty(arg))
            {
                var constantArg = new TypeName(arg) {FullyQualifiedName = arg};
                args.Add(constantArg);
            }
            else if (templist.StartsWith("*", StringComparison.Ordinal))
            {
                rest = templist.Substring(1).Trim();
                args.Add(TypeName.ANY_TYPE);
            }
            else if ((t = MatchTypeName(templist, out rest)) != null)
            {
                args.Add(t);
            }
            else
            {
                return false;
            }

            if (rest.Length > 1 && rest[0] == ',')
            {
                return MatchTemplateList(rest.Substring(1).Trim(), out rest, args);
            }

            return true;
        }

        static bool MatchParameterList(string plist, out string rest, ICollection<TypeName> args)
        {
            rest = plist;
            while (rest.Length > 0 && rest[0] != ')')
            {
                TypeName t;
                if ((t = MatchTypeName(rest, out rest)) == null)
                {
                    return false;
                }

                args.Add(t);
                if (rest.Length > 1 && rest[0] == ',')
                {
                    rest = rest.Substring(1).Trim();
                }
            }

            return true;
        }

        TypeName()
        {
            Args = new List<TypeName>();
            Qualifiers = new List<TypeName>();
            Parameters = null;
            IsWildcard = false;
            IsArray = false;
        }

        TypeName(string name) : this()
        {
            BaseName = name;
        }

        void SetArraySize(int[] dimensions)
        {
            IsArray = true;
            Dimensions = dimensions;
        }
    }
}