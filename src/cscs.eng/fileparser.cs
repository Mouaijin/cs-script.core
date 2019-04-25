#region Licence...

//----------------------------------------------
// The MIT License (MIT)
// Copyright (c) 2004-2018 Oleg Shilo
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial
// portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//----------------------------------------------

#endregion Licence...

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using csscript;

namespace CSScriptLibrary
{
    /// <summary>
    /// ParsingParams is an class that holds parsing parameters (parameters that controls how file is to be parsed).
    /// At this moment they are namespace renaming rules only.
    /// </summary>
    class ParsingParams
    {
        public ParsingParams()
        {
            renameNamespaceMap = new List<string[]>();
        }

        public string[][] RenameNamespaceMap
        {
            get { return renameNamespaceMap.ToArray(); }
        }

        public void AddRenameNamespaceMap(string[][] names)
        {
            renameNamespaceMap.AddRange(names);
        }

        /// <summary>
        /// Compare() is to be used to help with implementation of IComparer for sorting operations.
        /// </summary>
        public static int Compare(ParsingParams xPrams, ParsingParams yPrams)
        {
            if (xPrams == null && yPrams == null)
                return 0;

            int retval = xPrams == null ? -1 : (yPrams == null ? 1 : 0);

            if (retval == 0)
            {
                string[][] xNames = xPrams.RenameNamespaceMap;
                string[][] yNames = yPrams.RenameNamespaceMap;
                retval = System.Collections.Comparer.Default.Compare(xNames.Length, yNames.Length);
                if (retval == 0)
                {
                    for (int i = 0; i < xNames.Length && retval == 0; i++)
                    {
                        retval = System.Collections.Comparer.Default.Compare(xNames[i].Length, yNames[i].Length);
                        if (retval == 0)
                        {
                            for (int j = 0; j < xNames[i].Length; j++)
                            {
                                retval = System.Collections.Comparer.Default.Compare(xNames[i][j], yNames[i][j]);
                            }
                        }
                    }
                }
            }
            return retval;
        }

        public bool preserveMain = false;
#if !class_lib
        public string importingErrorMessage;
#endif
        List<string[]> renameNamespaceMap;
    }

    /// <summary>
    /// Class which is a placeholder for general information of the script file
    /// </summary>
    class ScriptInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="info">ImportInfo object containing the information how the script file should be parsed.</param>
        public ScriptInfo(CSharpParser.ImportInfo info)
        {
            this.fileName = info.file;
#if !class_lib
            parseParams.importingErrorMessage = "Cannot import \"" + info.rawStatement + "\" from the \"" + info.parentScript + "\" script.";
#endif
            parseParams.AddRenameNamespaceMap(info.renaming);
            parseParams.preserveMain = info.preserveMain;
        }

        public ParsingParams parseParams = new ParsingParams();
        public string fileName;
    }

    /// <summary>
    /// Class that implements parsing the single C# script file
    /// </summary>
    class FileParser
    {
        static bool _throwOnError = true;

#if !class_lib

        public System.Threading.ApartmentState ThreadingModel
        {
            get
            {
                if (this.parser == null)
                    return System.Threading.ApartmentState.Unknown;
                else
                    return this.parser.ThreadingModel;
            }
        }

#endif

        public FileParser()
        {
        }

        public FileParser(string fileName, ParsingParams prams, bool process, bool imported, string[] searchDirs, bool throwOnError)
        {
            if (searchDirs == null)
                searchDirs = new string[0];

            FileParser._throwOnError = throwOnError;
            this.imported = imported;
            this.prams = prams;
            this.fileName = ResolveFile(fileName, searchDirs);
            this.searchDirs = searchDirs.ConcatWith(Path.GetDirectoryName(this.fileName))
                                        .RemovePathDuplicates();
            if (process)
                ProcessFile();
        }

        public string fileNameImported = "";
        public ParsingParams prams = null;

        public string FileToCompile
        {
            get { return imported ? fileNameImported : fileName; }
        }

        public string[] SearchDirs
        {
            get { return searchDirs; }
        }

        public bool Imported
        {
            get { return imported; }
        }

        public string[] ReferencedNamespaces
        {
            get { return parser.RefNamespaces.Except(parser.IgnoreNamespaces).ToArray(); }
        }

        public string[] IgnoreNamespaces
        {
            get { return parser.IgnoreNamespaces; }
        }

        public string[] Precompilers
        {
            get { return parser.Precompilers; }
        }

        public string[] ReferencedAssemblies
        {
            get { return parser.RefAssemblies; }
        }

        public string[] Packages
        {
            get { return parser.NuGets; }
        }

        public string[] ExtraSearchDirs
        {
            get { return parser.ExtraSearchDirs; }
        }

        public string[] ReferencedResources
        {
            get { return parser.ResFiles; }
        }

        public string[] CompilerOptions
        {
            get { return parser.CompilerOptions; }
        }

        public ScriptInfo[] ReferencedScripts
        {
            get { return referencedScripts.ToArray(); }
        }

        public void ProcessFile()
        {
            packages.Clear();
            referencedAssemblies.Clear();
            referencedScripts.Clear();
            referencedNamespaces.Clear();
            referencedResources.Clear();

            this.parser = new CSharpParser(fileName, true, null, this.searchDirs);

            foreach (CSharpParser.ImportInfo info in parser.Imports)
            {
                referencedScripts.Add(new ScriptInfo(info));
            }

            referencedAssemblies.AddRange(parser.RefAssemblies);
            referencedNamespaces.AddRange(parser.RefNamespaces.Except(parser.IgnoreNamespaces));
            referencedResources.AddRange(parser.ResFiles);
#if !class_lib
            if (imported)
            {
                if (prams != null)
                {
                    parser.DoRenaming(prams.RenameNamespaceMap, prams.preserveMain);
                }
                if (parser.ModifiedCode == "")
                {
                    fileNameImported = fileName; //importing does not require any modification of the original code
                }
                else
                {
                    fileNameImported = Path.Combine(CSExecutor.ScriptCacheDir, string.Format("i_{0}_{1}{2}", Path.GetFileNameWithoutExtension(fileName), CSSUtils.GetHashCodeEx(Path.GetDirectoryName(fileName)), Path.GetExtension(fileName)));
                    if (!Directory.Exists(Path.GetDirectoryName(fileNameImported)))
                        Directory.CreateDirectory(Path.GetDirectoryName(fileNameImported));
                    if (File.Exists(fileNameImported))
                    {
                        File.SetAttributes(fileNameImported, FileAttributes.Normal);
                        Utils.FileDelete(fileNameImported, true);
                    }

                    using (StreamWriter scriptWriter = new StreamWriter(fileNameImported, false, Encoding.UTF8))
                    {
                        //scriptWriter.Write(ComposeHeader(fileNameImported)); //using a big header at start is overkill (it also shifts line numbers so they do not match with the original script file)
                        //but might be required in future
                        scriptWriter.WriteLine(parser.ModifiedCode);
                        scriptWriter.WriteLine("///////////////////////////////////////////");
                        scriptWriter.WriteLine("// Compiler-generated file - DO NOT EDIT!");
                        scriptWriter.WriteLine("///////////////////////////////////////////");
                    }
                    File.SetAttributes(fileNameImported, FileAttributes.ReadOnly);
                }
            }
#endif
        }

        List<ScriptInfo> referencedScripts = new List<ScriptInfo>();
        List<string> referencedNamespaces = new List<string>();
        List<string> referencedAssemblies = new List<string>();
        List<string> packages = new List<string>();
        List<string> referencedResources = new List<string>();

        string[] searchDirs;
        bool imported = false;

        /// <summary>
        /// Searches for script file by given script name. Calls ResolveFile(string fileName, string[] extraDirs, bool throwOnError)
        /// with throwOnError flag set to true.
        /// </summary>
        public static string ResolveFile(string fileName, string[] extraDirs)
        {
            return ResolveFile(fileName, extraDirs, _throwOnError);
        }

        internal static string[] ResolveFiles(string fileName, string[] extraDirs)
        {
            return ResolveFiles(fileName, extraDirs, _throwOnError);
        }

        /// <summary>
        /// The resolve file algorithm,
        /// <para>
        /// The default algorithm searches for script file by given script name. Search order:
        /// 1. Current directory
        /// 2. extraDirs (usually %CSSCRIPT_ROOT%\Lib and ExtraLibDirectory)
        /// 3. PATH
        /// Also fixes file name if user did not provide extension for script file (assuming .cs extension)
        /// </para>
        /// </summary>
        internal static ResolveSourceFileAlgorithm ResolveFilesAlgorithm = ResolveFilesDefault;

        //internal static ResolveSourceFileHandler ResolveFileAlgorithm = ResolveFileDefault;

        /// <summary>
        /// Searches for script file by given script name. Search order:
        /// 1. Current directory
        /// 2. extraDirs (usually %CSSCRIPT_ROOT%\Lib and ExtraLibDirectory)
        /// 3. PATH
        /// Also fixes file name if user did not provide extension for script file (assuming .cs extension)
        /// <para>If the default implementation isn't suitable then you can set <c>FileParser.ResolveFilesAlgorithm</c>
        /// to the alternative implementation of the probing algorithm.</para>
        /// </summary>
        public static string ResolveFile(string file, string[] extraDirs, bool throwOnError)
        {
            string[] files = ResolveFilesAlgorithm(file, extraDirs, throwOnError);
            return files.Length > 0 ? files[0] : null;
        }

        internal static string[] ResolveFiles(string file, string[] extraDirs, bool throwOnError)
        {
            return ResolveFilesAlgorithm(file, extraDirs, throwOnError);
        }

        internal static string[] ResolveFilesDefault(string file, string[] extraDirs, bool throwOnError)
        {
            string[] retval = _ResolveFiles(file, extraDirs, "");
            if (retval.Length == 0)
                retval = _ResolveFiles(file, extraDirs, ".cs");
            if (retval.Length == 0)
                retval = _ResolveFiles(file, extraDirs, ".csl"); //script link file

            if (retval.Length == 0)
            {
                if (throwOnError)
                    throw new FileNotFoundException(string.Format("Could not find file \"{0}\".{1}Ensure it is in one of the CS-Script search/probing directories.", file, Environment.NewLine));

                if (!file.EndsWith(".cs"))
                    retval = new string[] { file + ".cs" };
                else
                    retval = new string[] { file };
            }

            return retval;
        }

        public static string[] LocateFiles(string dir, string file)
        {
            var filePath = dir.PathJoin(file);
            if (file.Contains('*') || file.Contains('?'))
            {
                var filePattern = filePath.GetFileName();
                var dirPattern = file.GetDirName();

                if (file == "**")
                {
                    filePattern = "*";
                    dirPattern = "**";
                }
                else if (dirPattern.StartsWith("." + Path.DirectorySeparatorChar))
                {
                    dirPattern = dirPattern.Substring(2);
                }

                var candidates = Directory.GetFiles(dir, filePattern, SearchOption.AllDirectories)
                                          .Select(x => x.Substring(dir.Length + 1));

                var result = new List<string>();
                foreach (var relativePath in candidates)
                {
                    var dirRelativePath = relativePath.GetDirName();

                    bool matching = dirPattern.WildCardToRegExp().IsMatch(dirRelativePath);

                    if (matching)
                        result.Add(dir.PathJoin(relativePath).GetFullPath());
                }
                return result.ToArray();
            }
            else
                try
                {
                    string searchDir = Path.GetDirectoryName(filePath);
                    string name = Path.GetFileName(filePath);

                    List<string> result = new List<string>();

                    if (Directory.Exists(dir))
                        foreach (string item in Directory.GetFiles(searchDir, name))
                            result.Add(Path.GetFullPath(item));

                    return result.ToArray();
                }
                catch { }

            return new string[0];
        }

        static string[] _ResolveFiles(string file, string[] extraDirs, string extension)
        {
            string fileName = file;

            //current directory
            if (Path.GetExtension(fileName) == "")
                fileName += extension;

            if (Path.IsPathRooted(fileName) && File.Exists(fileName))
                return new[] { fileName };

            string[] files;
            if (!extraDirs.Contains(Environment.CurrentDirectory))
            {
                files = LocateFiles(Environment.CurrentDirectory, fileName);
                if (files.Length > 0)
                    return files;
            }

            //arbitrary directories
            if (extraDirs != null)
            {
                foreach (string dir in extraDirs)
                {
                    files = LocateFiles(dir, fileName);
                    if (files.Any())
                        return files;
                }
            }

            //PATH
            string[] pathDirs = Environment.GetEnvironmentVariable("PATH").Replace("\"", "").Split(';');
            foreach (string dir in pathDirs)
            {
                files = LocateFiles(dir, fileName);
                if (files.Any())
                    return files;
            }

            return new string[0];
        }

        static public string headerTemplate =
               @"/*" + Environment.NewLine +
               @" Created by {0}" +
               @" Original location: {1}" + Environment.NewLine +
               @" C# source equivalent of {2}" + Environment.NewLine +
               @" compiler-generated file created {3} - DO NOT EDIT!" + Environment.NewLine +
               @"*/" + Environment.NewLine;

        public string ComposeHeader(string path)
        {
#if class_lib
            return string.Format(headerTemplate, "CS-Script", path, fileName, DateTime.Now);
#else
            return string.Format(headerTemplate, csscript.AppInfo.appLogoShort, path, fileName, DateTime.Now);
#endif
        }

        public string fileName = "";
        public CSharpParser parser;
    }

    /// <summary>
    /// Class that implements parsing the single C# Script file
    /// </summary>
    /// <summary>
    /// Implementation of the IComparer for sorting operations of collections of FileParser instances
    /// </summary>
    ///
    class FileParserComparer : IComparer<FileParser>
    {
        public int Compare(FileParser x, FileParser y)
        {
            if (x == null && y == null)
                return 0;

            int retval = x == null ? -1 : (y == null ? 1 : 0);

            if (retval == 0)
            {
                retval = string.Compare(x.fileName, y.fileName, true);
                if (retval == 0)
                {
                    retval = ParsingParams.Compare(x.prams, y.prams);
                }
            }

            return retval;
        }
    }
}