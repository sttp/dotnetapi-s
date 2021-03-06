﻿//******************************************************************************************************
//  FilePath.cs - Gbtc
//
//  Copyright © 2019, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  04/12/2019 - J. Ritchie Carroll
//       Imported source code from Grid Solutions Framework.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace sttp
{
    /// <summary>
    /// Contains File and Path manipulation methods.
    /// </summary>
    public static class FilePath
    {
        #region [ Members ]

        // Fields
        private static readonly string s_directorySeparatorCharPattern;
        private static readonly string s_fileNameCharPattern;

        #endregion

        #region [ Constructor ]

        static FilePath()
        {
            char[] directorySeparatorChars =
            {
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            };

            char[] encodedInvalidFileNameChars = Path.GetInvalidFileNameChars()
                .SelectMany(c => c.RegexEncode())
                .ToArray();

            char[] encodedDirectorySeparatorChars = directorySeparatorChars
                .Distinct()
                .SelectMany(c => c.RegexEncode())
                .ToArray();

            // Defines a regular expression pattern for a valid directory separator character.
            s_directorySeparatorCharPattern = $"[{new string(encodedDirectorySeparatorChars)}]";

            // Defines a regular expression pattern for a valid file name character. We do this by
            // allowing any characters except those that would not be valid as part of a filename.
            // This essentially builds the "?" wildcard pattern match.
            s_fileNameCharPattern = $"[^{new string(encodedInvalidFileNameChars)}]";
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Gets the absolute file path for the specified file name or relative file path.
        /// </summary>
        /// <param name="filePath">File name or relative file path.</param>
        /// <returns>Absolute file path for the specified file name or relative file path.</returns>
        public static string GetAbsolutePath(string filePath)
        {
            if (!Path.IsPathRooted(filePath))
            {
                try
                {
                    filePath = Path.Combine(GetDirectoryName(AssemblyInfo.EntryAssembly.Location), filePath);
                }
                catch
                {
                    // Fall back on executing assembly path if entry assembly is not available
                    filePath = Path.Combine(GetDirectoryName(AssemblyInfo.ExecutingAssembly.Location), filePath);
                }
            }

            return RemovePathSuffix(filePath);
        }

        /// <summary>
        /// Returns the names of the subdirectories (including their paths) that match the specified search pattern in the specified directory, and optionally searches subdirectories.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchPattern">The search string to match against the names of subdirectories in <paramref name="path"/>. This parameter can contain a combination of valid literal and wildcard characters, but doesn't support regular expressions.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include all subdirectories or only the current directory.</param>
        /// <param name="exceptionHandler">Handles exceptions thrown during directory enumeration.</param>
        /// <returns>An array of the full names (including paths) of the subdirectories that match the specified criteria, or an empty array if no directories are found.</returns>
        public static string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories, Action<Exception> exceptionHandler = null)
        {
            return EnumerateDirectories(path, searchPattern, searchOption, exceptionHandler).ToArray();
        }

        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchPattern">The search string to match against the names of directories in <paramref name="path"/>. This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but doesn't support regular expressions.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or should include all subdirectories.</param>
        /// <param name="exceptionHandler">Handles exceptions thrown during directory enumeration.</param>
        /// <returns>An enumerable collection of the full names (including paths) for the directories in the directory specified by <paramref name="path"/> and that match the specified search pattern and option.</returns>
        public static IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories, Action<Exception> exceptionHandler = null)
        {
            try
            {
                return searchOption == SearchOption.TopDirectoryOnly
                    ? Directory.EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly)
                    : Directory.EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly)
                        .Concat(Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly)
                            .SelectMany(directory => EnumerateDirectories(directory, searchPattern, searchOption, exceptionHandler)));
            }
            catch (Exception ex)
            {
                exceptionHandler?.Invoke(ex);
            }

            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Returns the names of files (including their paths) that match the specified search pattern in the specified directory, using a value to determine whether to search subdirectories.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchPattern">The search string to match against the names of files in <paramref name="path"/>. This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but doesn't support regular expressions.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include all subdirectories or only the current directory.</param>
        /// <param name="exceptionHandler">Handles exceptions thrown during file enumeration.</param>
        /// <returns>An array of the full names (including paths) for the files in the specified directory that match the specified search pattern and option, or an empty array if no files are found.</returns>
        public static string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories, Action<Exception> exceptionHandler = null)
        {
            return EnumerateFiles(path, searchPattern, searchOption, exceptionHandler).ToArray();
        }

        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchPattern">The search string to match against the names of files in <paramref name="path"/>. This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but doesn't support regular expressions.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or should include all subdirectories.</param>
        /// <param name="exceptionHandler">Handles exceptions thrown during file enumeration.</param>
        /// <returns>An enumerable collection of the full names (including paths) for the files in the directory specified by <paramref name="path"/> and that match the specified search pattern and option.</returns>
        public static IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories, Action<Exception> exceptionHandler = null)
        {
            try
            {
                return searchOption == SearchOption.TopDirectoryOnly
                    ? Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly)
                    : Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly)
                        .Concat(Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly)
                            .SelectMany(directory => EnumerateFiles(directory, searchPattern, searchOption, exceptionHandler)));
            }
            catch (Exception ex)
            {
                exceptionHandler?.Invoke(ex);
            }

            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Ensures the supplied path name is valid.
        /// </summary>
        /// <param name="filePath">The file path to be validated.</param>
        /// <remarks>
        /// Throws argument exceptions if the <see param="pathName"/> is invalid.
        /// </remarks>
        public static void ValidatePathName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "Path cannot be null or empty space");

            if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new ArgumentException("Path has invalid characters.", nameof(filePath));
        }

        /// <summary>
        /// Determines whether the specified file name matches any of the given file specs (wildcards are defined as '*' or '?' characters).
        /// </summary>
        /// <param name="fileSpecs">The file specs used for matching the specified file name.</param>
        /// <param name="filePath">The file path to be tested against the specified file specs for a match.</param>
        /// <param name="ignoreCase"><c>true</c> to specify a case-insensitive match; otherwise <c>false</c>.</param>
        /// <returns><c>true</c> if the specified file name matches any of the given file specs; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> contains one or more of the invalid characters defined in <see cref="Path.GetInvalidPathChars"/>.</exception>
        /// <remarks>
        /// The syntax for <paramref name="fileSpecs"/> adheres to the following rules:
        /// 
        /// <ul>
        /// <li>Either '\' or '/' (as defined by <see cref="Path.DirectorySeparatorChar"/> and <see cref="Path.AltDirectorySeparatorChar"/>) can match the other.</li>
        /// <li>A single '\' or '/' at the beginning of the pattern matches any valid path root (such as "C:\" or "\\server\share").</li>
        /// <li>A '?' matches a single character which would be valid in a file name (as defined by <see cref="Path.GetInvalidFileNameChars"/>).</li>
        /// <li>A '*' matches any number of characters which would be valid in a file name.</li>
        /// <li>A sequence of "**\" or "**/" matches any number of sequential directories.</li>
        /// <li>Any other character matches itself.</li>
        /// </ul>
        /// </remarks>
        public static bool IsFilePatternMatch(string[] fileSpecs, string filePath, bool ignoreCase)
        {
            foreach (string fileSpec in fileSpecs)
            {
                if (IsFilePatternMatch(fileSpec, filePath, ignoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified file name matches the given file spec (wildcards are defined as '*' or '?' characters).
        /// </summary>
        /// <param name="fileSpec">The file spec used for matching the specified file name.</param>
        /// <param name="filePath">The file path to be tested against the specified file spec for a match.</param>
        /// <param name="ignoreCase"><c>true</c> to specify a case-insensitive match; otherwise <c>false</c>.</param>
        /// <returns><c>true</c> if the specified file name matches the given file spec; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> contains one or more of the invalid characters defined in <see cref="Path.GetInvalidPathChars"/>.</exception>
        /// <remarks>
        /// The syntax for <paramref name="fileSpec"/> adheres to the following rules:
        /// 
        /// <ul>
        /// <li>Either '\' or '/' (as defined by <see cref="Path.DirectorySeparatorChar"/> and <see cref="Path.AltDirectorySeparatorChar"/>) can match the other.</li>
        /// <li>A single '\' or '/' at the beginning of the pattern matches any valid path root (such as "C:\" or "\\server\share").</li>
        /// <li>A '?' matches a single character which would be valid in a file name (as defined by <see cref="Path.GetInvalidFileNameChars"/>).</li>
        /// <li>A '*' matches any number of characters which would be valid in a file name.</li>
        /// <li>A sequence of "**\" or "**/" matches any number of sequential directories.</li>
        /// <li>Any other character matches itself.</li>
        /// </ul>
        /// </remarks>
        public static bool IsFilePatternMatch(string fileSpec, string filePath, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            // Define regular expression patterns for the three possible sequences that can match the path root.
            string recursiveDirPattern = $"{Regex.Escape("**")}{s_directorySeparatorCharPattern}";
            string pathRootPattern = string.Format("({0}[^{0}]|{0}$)", Regex.Escape(Path.DirectorySeparatorChar.ToString()));
            string altPathRootPattern = string.Format("({0}[^{0}]|{0}$)", Regex.Escape(Path.AltDirectorySeparatorChar.ToString()));

            // If any of the three patterns are found at the start of fileSpec and the fileName refers to a rooted path,
            // remove the path root from fileName and then remove all leading directory separator chars from both fileSpec and fileName.
            if (Regex.IsMatch(fileSpec, $"^({recursiveDirPattern}|{pathRootPattern}|{altPathRootPattern})") && Path.IsPathRooted(filePath))
            {
                fileSpec = fileSpec.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                filePath = Regex.Replace(filePath, $"^{Regex.Escape(Path.GetPathRoot(filePath))}", "").TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            // Use regular expression matching to determine whether fileSpec matches fileName.
            return new Regex(GetFilePatternRegularExpression(fileSpec), ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None).IsMatch(filePath);
        }

        /// <summary>
        /// Determines if the specified file name and path is valid.
        /// </summary>
        /// <param name="filePath">The file name with optional path to test for validity.</param>
        /// <returns><c>true</c> if the specified <paramref name="filePath"/> is a valid name; otherwise <c>false</c>.</returns>
        public static bool IsValidFileName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                // Attempt to parse directory and file name - this will catch most issues
                string directory = Path.GetDirectoryName(filePath);
                string filename = Path.GetFileName(filePath);

                // Check for invalid directory characters
                if (!string.IsNullOrWhiteSpace(directory) && directory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    return false;

                // Check for invalid file name characters
                if (string.IsNullOrWhiteSpace(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return false;

                // Recurse in to check validity of each directory name
                if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(Path.GetFileName(directory)))
                    return IsValidFileName(directory);
            }
            catch
            {
                // If you can't parse a file name or directory, file path is definitely not valid
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a valid file name by replacing invalid file name characters with <paramref name="replaceWithCharacter"/>.
        /// </summary>
        /// <param name="fileName">File name to validate.</param>
        /// <param name="replaceWithCharacter">Character to replace invalid file name characters with. Set to '\0' to remove invalid file name characters.</param>
        /// <returns>A valid file name with no invalid file name characters.</returns>
        /// <remarks>
        /// This function expects a file name, not a file name with a path. To properly get a valid file path, where all directory
        /// names and the file name are validated, use the <see cref="GetValidFilePath"/>. Calling the <see cref="GetValidFileName"/>
        /// function will a full path will yield all directory separators replaced with the <paramref name="replaceWithCharacter"/>.
        /// </remarks>
        public static string GetValidFileName(string fileName, char replaceWithCharacter = '_')
        {
            if (replaceWithCharacter == '\0')
                return fileName.RemoveInvalidFileNameCharacters();

            return fileName.ReplaceInvalidFileNameCharacters(replaceWithCharacter);
        }

        /// <summary>
        /// Gets a valid file path by replacing invalid file or directory name characters with <paramref name="replaceWithCharacter"/>.
        /// </summary>
        /// <param name="filePath">File path to validate.</param>
        /// <param name="replaceWithCharacter">Character to replace invalid file or directory name characters with. Set to '\0' to remove invalid file or directory name characters.</param>
        /// <returns>A valid file path with no invalid file or directory name characters.</returns>
        public static string GetValidFilePath(string filePath, char replaceWithCharacter = '_')
        {
            string[] fileParts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            for (int i = 0; i < fileParts.Length; i++)
            {
                // Leave any volume specification alone
                if (i == 0 &&
                    Path.VolumeSeparatorChar != Path.DirectorySeparatorChar &&
                    Path.VolumeSeparatorChar != Path.AltDirectorySeparatorChar &&
                    fileParts[0].IndexOfAny(new[] { Path.VolumeSeparatorChar }) > 0)
                    continue;

                fileParts[i] = GetValidFileName(fileParts[i], replaceWithCharacter);
            }

            return string.Join(Path.DirectorySeparatorChar.ToString(), fileParts);
        }

        /// <summary>
        /// Gets the file name and extension from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path from which the file name and extension is to be obtained.</param>
        /// <returns>File name and extension if the file path has it; otherwise empty string.</returns>
        public static string GetFileName(string filePath)
        {
            return Path.GetFileName(filePath);
        }

        /// <summary>
        /// Gets the extension from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path from which the extension is to be obtained.</param>
        /// <returns>File extension.</returns>
        public static string GetExtension(string filePath)
        {
            return Path.GetExtension(RemovePathSuffix(filePath));
        }

        /// <summary>
        /// Gets the file name without extension from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path from which the file name is to be obtained.</param>
        /// <returns>File name without the extension if the file path has it; otherwise empty string.</returns>
        public static string GetFileNameWithoutExtension(string filePath)
        {
            return Path.GetFileNameWithoutExtension(RemovePathSuffix(filePath));
        }

        /// <summary>
        /// Gets a unique file path for the given file by checking for name collisions and
        /// adding a sequence number to the end of the file name if there is a collision.
        /// </summary>
        /// <param name="originalFilePath">The path to the original file before adding the sequence number.</param>
        /// <returns>The unique path to the file.</returns>
        /// <remarks>
        /// This method is designed to handle the case where the user wishes to create a file in a folder
        /// with a given name when there is a possibility that the name is already taken. Using this method,
        /// it is possible to create files with names in the following format:
        /// 
        /// <ul>
        ///     <li>File.ext</li>
        ///     <li>File (1).ext</li>
        ///     <li>File (2).ext</li>
        ///     <li>...</li>
        /// </ul>
        /// 
        /// This method uses a binary search to find a unique file name, so it is suitable for situations where
        /// a large number of files will be created with the same file name, and the next unique file name needs
        /// to be found relatively quickly. It will not always detect gaps in the sequence numbers that can occur
        /// when files are deleted (for instance, if "File (1).ext" were deleted from the list above).
        /// </remarks>
        public static string GetUniqueFilePathWithBinarySearch(string originalFilePath)
        {
            string uniqueFilePath = GetAbsolutePath(originalFilePath);
            string directory = GetDirectoryName(uniqueFilePath);
            string originalFileRoot = GetFileNameWithoutExtension(uniqueFilePath);
            string fileExtension = GetExtension(uniqueFilePath);

            int i = 1;
            int j = 1;
            int k = 1;

            if (!File.Exists(uniqueFilePath))
                return uniqueFilePath;

            while (File.Exists(uniqueFilePath))
            {
                uniqueFilePath = Path.Combine(directory, $"{originalFileRoot} ({i}){fileExtension}");
                j = k;
                k = i;
                i *= 2;
            }

            while (j < k)
            {
                i = (j + k) / 2;
                uniqueFilePath = Path.Combine(directory, $"{originalFileRoot} ({i}){fileExtension}");

                if (File.Exists(uniqueFilePath))
                    j = i + 1;
                else
                    k = i;
            }

            return Path.Combine(directory, $"{originalFileRoot} ({k}){fileExtension}");
        }

        /// <summary>
        /// Gets the size of the specified file.
        /// </summary>
        /// <param name="fileName">Name of file whose size is to be retrieved.</param>
        /// <returns>The size of the specified file.</returns>
        public static long GetFileLength(string fileName)
        {
            try
            {
                return new FileInfo(fileName).Length;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets a list of files under the specified path. Search wildcard pattern (c:\Data\*.dat) can be used for 
        /// including only the files matching the pattern or path wildcard pattern (c:\Data\*\*.dat) to indicate the 
        /// inclusion of files under all subdirectories in the list.
        /// </summary>
        /// <param name="path">The path for which a list of files is to be returned.</param>
        /// <param name="exceptionHandler">Handles exceptions thrown during file enumeration.</param>
        /// <returns>A list of files under the given path.</returns>
        public static string[] GetFileList(string path, Action<Exception> exceptionHandler = null)
        {
            string directory = GetDirectoryName(path);
            string filePattern = GetFileName(path);
            SearchOption options = SearchOption.TopDirectoryOnly;

            if (string.IsNullOrEmpty(filePattern))
            {
                // No wildcard pattern was specified, so get a listing of all files.
                filePattern = "*.*";
            }

            if (GetLastDirectoryName(directory) == "*")
            {
                // Path wildcard pattern is used to specify the option to include subdirectories.
                options = SearchOption.AllDirectories;
                directory = directory.Remove(directory.LastIndexOf("*", StringComparison.OrdinalIgnoreCase));
            }

            if ((object)exceptionHandler == null)
                return Directory.GetFiles(directory, filePattern, options);

            return GetFiles(directory, filePattern, options, exceptionHandler);
        }

        /// <summary>
        /// Gets a regular expression pattern that simulates wildcard matching for filenames (wildcards are defined as '*' or '?' characters).
        /// </summary>
        /// <param name="fileSpec">The file spec for which the regular expression pattern if to be generated.</param>
        /// <returns>Regular expression pattern that simulates wildcard matching for filenames.</returns>
        public static string GetFilePatternRegularExpression(string fileSpec)
        {
            List<Tuple<string, string>> replacements = new List<Tuple<string, string>>()
            {
                // Replaces directory separator characters with their equivalent regular expressions.
                Tuple.Create($"{s_directorySeparatorCharPattern}+", $"{s_directorySeparatorCharPattern}+"),
                
                // Replaces wildcard file patterns with their equivalent regular expression.
                Tuple.Create(Regex.Escape("?"), s_fileNameCharPattern),
                Tuple.Create(Regex.Escape("**") + s_directorySeparatorCharPattern, $"({s_fileNameCharPattern}*{s_directorySeparatorCharPattern})*"),
                Tuple.Create(Regex.Escape("*"), $"{s_fileNameCharPattern}*")
            };

            StringBuilder input;
            StringBuilder output;
            Tuple<string, string> replacement;
            Match match;
            
            input = new StringBuilder(fileSpec);
            output = new StringBuilder();

            while (input.Length > 0)
            {
                // Determine if any of the replacement patterns match the input.
                replacement = replacements.FirstOrDefault(r => Regex.IsMatch(input.ToString(), $"^{r.Item1}"));

                if ((object)replacement != null)
                {
                    // If one of the replacement patterns matches the input,
                    // apply that replacement and write it to the output.
                    match = Regex.Match(input.ToString(), $"^{replacement.Item1}");
                    output.Append(Regex.Replace(match.Value, replacement.Item1, replacement.Item2));
                    input.Remove(0, match.Length);
                }
                else
                {
                    // If not, move a single character as-is from input to the output.
                    output.Append(Regex.Escape(input[0].ToString()));
                    input.Remove(0, 1);
                }
            }

            // Return the output string as the regular expression pattern.
            return "^" + output + "$";
        }

        /// <summary>
        /// Gets the directory information from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path from which the directory information is to be obtained.</param>
        /// <returns>Directory information.</returns>
        /// <remarks>
        /// This differs from <see cref="Path.GetDirectoryName"/> in that it will see if last name in path is
        /// a directory and, if it exists, will properly treat that part of path as a directory. The .NET path
        /// function always assumes last entry is a file name if path is not suffixed with a slash. For example:
        ///     Path.GetDirectoryName(@"C:\Music") will return "C:\", however, 
        /// FilePath.GetDirectoryName(@"C:\Music") will return "C:\Music\", so long as Music directory exists.
        /// </remarks>
        public static string GetDirectoryName(string filePath)
        {
            // Test for case where valid path does not end in directory separator, Path.GetDirectoryName assumes
            // this is a file name - whether is exists or not :-(
            string directoryName = AddPathSuffix(filePath);

            if (Directory.Exists(directoryName))
                return directoryName;

            return AddPathSuffix(Path.GetDirectoryName(filePath) ?? filePath);
        }

        /// <summary>
        /// Gets the last directory name from a file path.
        /// </summary>
        /// <param name="filePath">The file path from where the last directory name is to be retrieved.</param>
        /// <returns>The last directory name from a file path.</returns>
        /// <remarks>
        /// <see cref="GetLastDirectoryName(string)"/> would return sub2 from c:\windows\sub2\filename.ext.
        /// </remarks>
        public static string GetLastDirectoryName(string filePath)
        {
            // Test case should verify the following:
            //   FilePath.GetLastDirectoryName(@"C:\Test\sub") == "Test" <-- sub assumed to be filename
            //   FilePath.GetLastDirectoryName(@"C:\Test\sub\") == "sub" <-- sub assumed to be directory

            if (!string.IsNullOrEmpty(filePath))
            {
                int index;
                char[] dirVolChars = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar };

                // Remove file name and trailing directory separator character from the file path.
                filePath = RemovePathSuffix(GetDirectoryName(filePath));

                // Keep going through the file path until all directory separator characters are removed.
                while ((index = filePath.IndexOfAny(dirVolChars)) > -1)
                    filePath = filePath.Substring(index + 1);

                return filePath;
            }

            throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// Makes sure path is suffixed with standard <see cref="Path.DirectorySeparatorChar"/>.
        /// </summary>
        /// <param name="filePath">The file path to be suffixed.</param>
        /// <returns>Suffixed path.</returns>
        public static string AddPathSuffix(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.DirectorySeparatorChar.ToString();
            }
            else
            {
                char suffixChar = filePath[filePath.Length - 1];

                if (suffixChar != Path.DirectorySeparatorChar && suffixChar != Path.AltDirectorySeparatorChar)
                    filePath += Path.DirectorySeparatorChar;
            }

            return filePath;
        }

        /// <summary>
        /// Makes sure path is not suffixed with <see cref="Path.DirectorySeparatorChar"/> or <see cref="Path.AltDirectorySeparatorChar"/>.
        /// </summary>
        /// <param name="filePath">The file path to be unsuffixed.</param>
        /// <returns>Unsuffixed path.</returns>
        public static string RemovePathSuffix(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = "";
            }
            else
            {
                char suffixChar = filePath[filePath.Length - 1];

                while ((suffixChar == Path.DirectorySeparatorChar || suffixChar == Path.AltDirectorySeparatorChar) && filePath.Length > 0)
                {
                    filePath = filePath.Substring(0, filePath.Length - 1);

                    if (filePath.Length > 0)
                        suffixChar = filePath[filePath.Length - 1];
                }
            }

            return filePath;
        }

        /// <summary>
        /// Remove any path root present in the path.
        /// </summary>
        /// <param name="filePath">The file path whose root is to be removed.</param>
        /// <returns>The path with the root removed if it was present.</returns>
        public static string DropPathRoot(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "";

            return filePath.Remove(0, Path.GetPathRoot(filePath).Length);
        }

        /// <summary>
        /// Returns a file name, for display purposes, of the specified length using "..." to indicate a longer name.
        /// </summary>
        /// <param name="filePath">The file path to be trimmed.</param>
        /// <param name="length">The maximum length of the trimmed file path.</param>
        /// <returns>Trimmed file path.</returns>
        /// <remarks>
        /// Minimum value for the <paramref name="length" /> parameter is 12. 12 will be used for any value 
        /// specified as less than 12.
        /// </remarks>
        public static string TrimFileName(string filePath, int length)
        {
            if (string.IsNullOrEmpty(filePath))
                filePath = "";
            else
                filePath = filePath.Trim();

            if (length < 12)
                length = 12;

            if (filePath.Length > length)
            {
                string justName = GetFileName(filePath);

                if (justName.Length == filePath.Length)
                {
                    // This is just a file name. Make sure extension shows.
                    string justExtension = GetExtension(filePath);
                    string trimName = GetFileNameWithoutExtension(filePath);

                    if (trimName.Length > 8)
                    {
                        if (justExtension.Length > length - 8)
                            justExtension = justExtension.Substring(0, length - 8);

                        double offsetLen = (length - justExtension.Length - 3) / 2.0D;

                        return trimName.Substring(0, (int)Math.Ceiling(offsetLen)) + "..." +
                            trimName.Substring((int)Math.Round(trimName.Length - Math.Floor(offsetLen) + 1.0D)) + justExtension;
                    }

                    // We can not trim file names less than 8 with a "...", so we truncate long extension.
                    return trimName + justExtension.Substring(0, length - trimName.Length);
                }

                // File name alone exceeds length - recurses into function without path.
                if (justName.Length > length)
                    return TrimFileName(justName, length);

                // File name contains path. Trims path before file name.
                string justFilePath = GetDirectoryName(filePath);
                int offset = length - justName.Length - 4;

                if (justFilePath.Length > offset && offset > 0)
                    return justFilePath.Substring(0, offset) + "..." + Path.DirectorySeparatorChar + justName;

                // Can not fit path. Trims file name.
                return TrimFileName(justName, length);
            }

            // Full file name fits within requested length.
            return filePath;
        }

        #endregion
    }
}