using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dragonstone;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DivineDragon
{
    public static class CBT
    {
        public static bool Initialized { get; private set; }

        private static Dictionary<string, string> InternalIdCache { get; set; }

        // [InitializeOnLoadMethod]
        // private static void InitializeCatalogOnLoad()
        // {
        //     if (File.Exists("Assets/Share/AddressableAssetsData/TempCatalogFolder/catalog.json"))
        //     {
        //         if (!LoadCatalogContent("Assets/Share/AddressableAssetsData/TempCatalogFolder/catalog.json"))
        //         {
        //             Debug.LogError("CBT failed to load catalog.json on load even though it exists. Corrupted/Edited?");
        //         }
        //     }
        //     else
        //     {
        //         Debug.LogWarning("Divine Dragon Core settings are not configured yet. Consider configuring them to use the entire suite of tools.");
        //     }
        // }
        
        /// <summary>
        /// Loads the catalog file from Fire Emblem Engage to fetch file information and dependencies
        /// </summary>
        /// <param name="catalogPath">Path to the game's catalog file, extracted as a JSON</param>
        /// <returns>Returns true if the catalog loaded successfully</returns>
        public static bool LoadCatalogContent(string catalogPath)
        {
            if (Initialized)
                return true;
            
            if (string.IsNullOrEmpty(catalogPath))
            {
                Debug.LogError("catalogPath is not set");
                return false;
            }
            
            Debug.Log("Starting catalog loading");
            var handle = Addressables.LoadContentCatalogAsync(catalogPath, false);
            // Editor support for async is lacking so we just work sync.
            handle.WaitForCompletion();
            
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                 Debug.Log("Successfully loaded Engage's catalog.json ");

                 // Build a cache of lowercase InternalId to actual InternalId to facilitate matching physical files to addresses
                 InternalIdCache = Addressables.ResourceLocators.ToArray()[2].Keys.OfType<string>()
                     .Where(s => !s.StartsWith("fe_assets_") && !(Guid.TryParseExact(s, "D", out _) && !Path.HasExtension(s))).ToDictionary(x => x.ToLower(), x => x);
                 
                 Addressables.Release(handle);
                 Initialized = true;
                 return true;
            }
            
            Addressables.Release(handle);
            
            Debug.LogError("Could not load catalog.json file from Fire Emblem Engage");
            return false;
        }

        public static string PathToInternalId(string path)
        {
            // Turn the path into a lowercase InternalId
            // TODO: Do something about fe_scenes_
            string processed_path = Path.ChangeExtension(path.Replace(EngageAddressableSettings.GameBuildPath + "/fe_assets_", "").Replace(EngageAddressableSettings.GameBuildPath + "/fe_scenes_", ""), null);
            Debug.Log(processed_path);

            if (InternalIdCache.TryGetValue(processed_path, out string internalId))
            {
                Debug.Log($"Found matching InternalId: {internalId}");
                return internalId;
            }
            
            Debug.LogError($"Could not find matching InternalId: {processed_path}");
            return string.Empty;
        }

        /// <summary>
        /// Provides the dependencies for a specific key if found in the game's catalog
        /// </summary>
        /// <param name="key">The address to the asset to extract dependencies for</param>
        /// <returns>Returns a IEnumerable of absolute paths to the dependencies</returns>
        public static IEnumerable<string> GetDependenciesForAsset(string key)
        {
            if (!Initialized)
            {
                Debug.LogError("CBT library is not initialized");
                return new List<string>();
            }
            
            AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(key);
            handle.WaitForCompletion();
            
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var dependencies = handle.Result.First().Dependencies.Select(x => x.InternalId).Select(x => x.Replace(Addressables.BuildPath, Dragonstone.EngageAddressableSettings.GameRuntimePath));
                return dependencies;
            }
            else
            {
                Debug.Log("Fail");
            }
                
            return new List<string>();
        }
        
        private static void TraverseDependencies(IResourceLocation location, HashSet<IResourceLocation> visited, HashSet<string> paths)
        {
            if (location == null || visited.Contains(location))
                return;

            visited.Add(location);

            string resolvedPath = location.InternalId.Replace(
                Addressables.BuildPath,
                Dragonstone.EngageAddressableSettings.GameRuntimePath
            );

            paths.Add(resolvedPath);

            if (location.HasDependencies)
            {
                foreach (var dep in location.Dependencies)
                {
                    TraverseDependencies(dep, visited, paths);
                }
            }
        }
    }
}