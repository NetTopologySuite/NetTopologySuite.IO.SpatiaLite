// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See ACKNOWLEDGEMENTS.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyModel;
using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace NetTopologySuite.IO.SpatiaLite.Test
{
    /// <summary>
    ///     Finds and loads SpatiaLite.
    /// </summary>
    public static class SpatialiteLoader
    {
        private static readonly string _sharedLibraryExtension;
        private static readonly string _pathVariableName;

        private static string _extension;

        static SpatialiteLoader()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _sharedLibraryExtension = ".dll";
                _pathVariableName = "PATH";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _sharedLibraryExtension = ".dylib";
                _pathVariableName = "DYLD_LIBRARY_PATH";
            }
            else
            {
                _sharedLibraryExtension = ".so";
                _pathVariableName = "LD_LIBRARY_PATH";
            }
        }

        public static string FindExtension()
        {
            if (_extension != null)
            {
                return _extension;
            }

            bool hasDependencyContext;
            try
            {
                hasDependencyContext = DependencyContext.Default != null;
            }
            catch (Exception ex) // Work around dotnet/core-setup#4556
            {
                Debug.Fail(ex.ToString());
                hasDependencyContext = false;
            }

            if (hasDependencyContext)
            {
                var candidateAssets = new Dictionary<string, int>();
                string rid = RuntimeEnvironment.GetRuntimeIdentifier();
                var rids = DependencyContext.Default.RuntimeGraph.First(g => g.Runtime == rid).Fallbacks.ToList();
                rids.Insert(0, rid);

                foreach (var library in DependencyContext.Default.RuntimeLibraries)
                {
                    foreach (var group in library.NativeLibraryGroups)
                    {
                        foreach (string filePath in group.AssetPaths)
                        {
                            if (string.Equals(
                                Path.GetFileName(filePath),
                                "mod_spatialite" + _sharedLibraryExtension,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                int fallbacks = rids.IndexOf(group.Runtime);
                                if (fallbacks != -1)
                                {
                                    candidateAssets.Add(library.Path + "/" + filePath, fallbacks);
                                }
                            }
                        }
                    }
                }

                string assetPath = candidateAssets.OrderBy(p => p.Value)
                    .Select(p => p.Key.Replace('/', Path.DirectorySeparatorChar)).FirstOrDefault();
                if (assetPath != null)
                {
                    string assetFullPath = null;
                    string[] probingDirectories = ((string)AppDomain.CurrentDomain.GetData("PROBING_DIRECTORIES"))
                        .Split(Path.PathSeparator);
                    foreach (string directory in probingDirectories)
                    {
                        string candidateFullPath = Path.Combine(directory, assetPath);
                        if (File.Exists(candidateFullPath))
                        {
                            assetFullPath = candidateFullPath;
                        }
                    }
                    Debug.Assert(assetFullPath != null);

                    string assetDirectory = Path.GetDirectoryName(assetFullPath);

                    string currentPath = Environment.GetEnvironmentVariable(_pathVariableName);
                    if (!currentPath.Split(Path.PathSeparator).Any(
                        p => string.Equals(
                            p.TrimEnd(Path.DirectorySeparatorChar),
                            assetDirectory,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        Environment.SetEnvironmentVariable(
                            _pathVariableName,
                            assetDirectory + Path.PathSeparator + currentPath);
                    }
                }
            }

            string extension = "mod_spatialite";

            // Workaround ericsink/SQLitePCL.raw#225
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                extension += _sharedLibraryExtension;
            }

            return _extension = extension;
        }
    }
}
