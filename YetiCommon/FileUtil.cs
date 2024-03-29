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
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;

namespace YetiCommon
{
    public class FileUtil
    {
        public static string RemoveQuotesFromPath(string quoted)
        {
            int quote1 = quoted.IndexOf('\"');
            int quote2 = quoted.LastIndexOf('\"');
            if (quote1 == -1 && quote2 == -1) return quoted;
            if (quote1 >= quote2)
            {
                throw new ArgumentException("Open quote in string '" + quoted + "'.");
            }
            return quoted.Substring(quote1 + 1, quote2 - quote1 - 1);
        }

        public static string RemoveTrailingSeparator(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        // Returns the combined path, using forward-slash as the directory separator.
        public static string PathCombineLinux(string directory, string fileName)
        {
            if (directory == null || fileName == null)
            {
                throw new ArgumentNullException((directory == null) ? "directory" : "fileName");
            }
            if ((directory.IndexOf('\0') != -1) || (fileName.IndexOf('\0') != -1))
            {
                throw new ArgumentException("Invalid character in path");
            }
            if (directory.Length == 0)
            {
                return fileName;
            }
            if (directory[directory.Length - 1] != '/')
            {
                return directory + '/' + fileName;
            }
            return directory + fileName;
        }

        /// <summary>
        /// Returns the absolute path for the specified path string.
        /// </summary>
        /// <param name="path">The file or directory to get absolute path information.</param>
        /// <param name="baseRoot">Relative paths will be based on this directory.</param>
        /// <returns></returns>
        public static string GetFullPath(string path, string baseRoot)
        {
            if (!Path.IsPathRooted(baseRoot))
            {
                throw new ArgumentException("Unable to get full path, base root is not rooted.");
            }
            // If the path is not rooted, combine it with the base root.
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(baseRoot, path);
            }
            // Use Path.GetFullPath to fully resolve the path, removing any relative bits.
            // ex. C:\test\..\test2\ => C:\test2\
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Returns the size of file.
        /// </summary>
        /// <param name="path">The file absolute path.</param>
        /// <returns>Size of file in bytes or zero in case of IOException.</returns>
        public static long GetFileSize(string path, IFileSystem fileSystem)
        {
            try
            {
                return fileSystem.FileInfo.FromFileName(path).Length;
            }
            catch (IOException e)
            {
                Trace.WriteLine($"Error reading size of file '{path}': {e}");
                return 0;
            }
        }
    }
}
