﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See ACKNOWLEDGEMENTS.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Extensions.DependencyModel;

using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace NetTopologySuite.IO.SpatiaLite
{
    /// <summary>
    ///     Finds and loads SpatiaLite.
    /// </summary>
    public static class SpatialiteLoader
    {
        private static readonly string _sharedLibraryExtension;
        private static readonly string _pathVariableName;

        private static bool _looked;

        static SpatialiteLoader()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _sharedLibraryExtension = ".dll";
                _pathVariableName = "PATH";
            }
            else
            {
                // NB: The PATH trick we use won't work on Linux. Changing LD_LIBRARY_PATH has
                //     no effect after the first library is loaded
                _looked = true;
            }
        }

        /// <summary>
        ///     <para>
        ///         Loads the mod_spatialite extension into the specified connection.
        ///     </para>
        ///     <para>
        ///         The extension will be loaded from native NuGet assets when available.
        ///     </para>
        /// </summary>
        /// <param name="connection"> The connection. </param>
        public static void Load(DbConnection connection)
        {
            FindExtension();

            if (connection is SQLiteConnection sqliteConnection)
            {
                sqliteConnection.LoadExtension("mod_spatialite");
            }
            else
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT load_extension('mod_spatialite');";
                command.ExecuteNonQuery();
            }
        }

        private static void FindExtension()
        {
            if (_looked)
            {
                return;
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
                var candidateAssets = new Dictionary<(string, string), int>();
                var rid = RuntimeEnvironment.GetRuntimeIdentifier();
                var rids = DependencyContext.Default.RuntimeGraph.FirstOrDefault(g => g.Runtime == rid)?.Fallbacks.ToList()
                    ?? new List<string>();
                rids.Insert(0, rid);

                foreach (var library in DependencyContext.Default.RuntimeLibraries)
                {
                    foreach (var group in library.NativeLibraryGroups)
                    {
                        foreach (var file in group.RuntimeFiles)
                        {
                            if (string.Equals(
                                Path.GetFileName(file.Path),
                                "mod_spatialite" + _sharedLibraryExtension,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                var fallbacks = rids.IndexOf(group.Runtime);
                                if (fallbacks != -1)
                                {
                                    candidateAssets.Add((library.Path, file.Path), fallbacks);
                                }
                            }
                        }
                    }
                }

                var assetPath = candidateAssets.OrderBy(p => p.Value)
                    .Select(p => p.Key).FirstOrDefault();
                if (assetPath != default)
                {
                    string assetDirectory = null;
                    if (File.Exists(Path.Combine(AppContext.BaseDirectory, assetPath.Item2)))
                    {
                        // NB: This enables framework-dependent deployments
                        assetDirectory = Path.Combine(
                            AppContext.BaseDirectory,
                            Path.GetDirectoryName(assetPath.Item2.Replace('/', Path.DirectorySeparatorChar)));
                    }
                    else
                    {
                        string assetFullPath = null;
                        var probingDirectories = ((string)AppDomain.CurrentDomain.GetData("PROBING_DIRECTORIES"))
                            .Split(Path.PathSeparator);
                        foreach (var directory in probingDirectories)
                        {
                            var candidateFullPath = Path.Combine(
                                directory,
                                (assetPath.Item1 + "/" + assetPath.Item2).Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(candidateFullPath))
                            {
                                assetFullPath = candidateFullPath;
                            }
                        }

                        Debug.Assert(assetFullPath != null, "assetFullPath is null");

                        assetDirectory = Path.GetDirectoryName(assetFullPath);
                    }

                    Debug.Assert(assetDirectory != null, "assetDirectory is null");

                    // GetEnvironmentVariable can sometimes return null when there is a race condition
                    // with another thread setting it. Therefore we do a bit of back off and retry here.
                    // Note that the result can be null if no path is set on the system.
                    var delay = 1;
                    var currentPath = Environment.GetEnvironmentVariable(_pathVariableName);
                    while (currentPath == null && delay < 1000)
                    {
                        Thread.Sleep(delay);
                        delay *= 2;
                        currentPath = Environment.GetEnvironmentVariable(_pathVariableName);
                    }

                    if (currentPath == null
                        || !currentPath.Split(Path.PathSeparator).Any(
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

            _looked = true;
        }
    }
}
