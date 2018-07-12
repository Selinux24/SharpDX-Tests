﻿using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Engine
{
    /// <summary>
    /// Scenery level
    /// </summary>
    [Serializable]
    public class ModularSceneryLevel
    {
        /// <summary>
        /// Objects auto identifier counter
        /// </summary>
        private static int ObjectsAutoId = 1;
        /// <summary>
        /// Objects base string for identification build
        /// </summary>
        private static string ObjectsAutoString = "__objauto__";

        /// <summary>
        /// Level name
        /// </summary>
        [XmlAttribute("name")]
        public string Name = null;
        /// <summary>
        /// Position
        /// </summary>
        [XmlIgnore]
        public Vector3 StartPosition = new Vector3(0, 0, 0);
        /// <summary>
        /// Position vector
        /// </summary>
        [XmlElement("start")]
        public string StartPositionText
        {
            get
            {
                return string.Format("{0} {1} {2}", StartPosition.X, StartPosition.Y, StartPosition.Z);
            }
            set
            {
                var floats = ModularSceneryExtents.Split(value);
                if (floats.Length == 3)
                {
                    StartPosition = new Vector3(floats);
                }
                else
                {
                    StartPosition = ModularSceneryExtents.ReadReservedWordsForPosition(value);
                }
            }
        }
        /// <summary>
        /// Looking vector
        /// </summary>
        [XmlIgnore]
        public Vector3 LookingVector = new Vector3(0, 0, 0);
        /// <summary>
        /// Looking vector
        /// </summary>
        [XmlElement("look")]
        public string LookingVectorText
        {
            get
            {
                return string.Format("{0} {1} {2}", LookingVector.X, LookingVector.Y, LookingVector.Z);
            }
            set
            {
                var floats = ModularSceneryExtents.Split(value);
                if (floats.Length == 3)
                {
                    LookingVector = new Vector3(floats);
                }
                else
                {
                    LookingVector = ModularSceneryExtents.ReadReservedWordsForDirection(value);
                }
            }
        }
        /// <summary>
        /// Assets map
        /// </summary>
        [XmlArray("map")]
        [XmlArrayItem("item", typeof(ModularSceneryAssetReference))]
        public ModularSceneryAssetReference[] Map = new ModularSceneryAssetReference[] { };
        /// <summary>
        /// Map objects
        /// </summary>
        [XmlArray("objects")]
        [XmlArrayItem("item", typeof(ModularSceneryObjectReference))]
        public ModularSceneryObjectReference[] Objects = new ModularSceneryObjectReference[] { };

        /// <summary>
        /// Populate objects empty ids
        /// </summary>
        public void PopulateObjectIds()
        {
            foreach (var item in Objects)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    item.Id = string.Format("{0}_{1}", ObjectsAutoString, ObjectsAutoId++);
                }
            }
        }
        /// <summary>
        /// Gets the instance counter dictionary
        /// </summary>
        /// <param name="assets">Asset list</param>
        /// <returns>Returns a dictionary with the instance count by unique asset name</returns>
        public Dictionary<string, int> GetMapInstanceCounters(ModularSceneryAssetDescription[] assets)
        {
            Dictionary<string, int> res = new Dictionary<string, int>();

            foreach (var item in this.Map)
            {
                var asset = assets
                    .Where(a => string.Equals(a.Name, item.AssetName, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (asset != null)
                {
                    var assetInstances = asset.GetInstanceCounters();
                    foreach (var key in assetInstances.Keys)
                    {
                        if (!res.ContainsKey(key))
                        {
                            res.Add(key, 0);
                        }

                        res[key] += assetInstances[key];
                    }
                }
            }

            return res;
        }
        /// <summary>
        /// Gets the instance counter dictionary
        /// </summary>
        /// <returns>Returns a dictionary with the instance count by unique asset name</returns>
        public Dictionary<string, int> GetObjectsInstanceCounters()
        {
            Dictionary<string, int> res = new Dictionary<string, int>();

            foreach (var item in this.Objects)
            {
                if (string.IsNullOrEmpty(item.AssetName))
                {
                    continue;
                }

                if (!res.ContainsKey(item.AssetName))
                {
                    res.Add(item.AssetName, 0);
                }

                res[item.AssetName] += 1;
            }

            return res;
        }

        /// <summary>
        /// Finds the asset reference by asset map id and asset id
        /// </summary>
        /// <param name="assets">Asset list</param>
        /// <param name="mapId">Asset map id</param>
        /// <param name="id">Asset id</param>
        /// <returns>Returns the asset reference</returns>
        public ModularSceneryAssetReference FindAssetInstance(ModularSceneryAssetDescription[] assets, string mapId, string id)
        {
            var res = assets
                .Where(a => this.Map.Count(m =>
                    string.Equals(m.Id, mapId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.AssetName, a.Name, StringComparison.OrdinalIgnoreCase)) > 0)
                .Select(a => a.Assets.Where(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault())
                .FirstOrDefault();

            return res;
        }
        /// <summary>
        /// Gets the first index of the asset in the current configuration
        /// </summary>
        /// <param name="assets">Asset list</param>
        /// <param name="assetName">Asset name</param>
        /// <param name="assetMapId">Asset map id</param>
        /// <param name="assetId">Asset id</param>
        /// <returns>Returns the first index</returns>
        public int GetMapInstanceIndex(ModularSceneryAssetDescription[] assets, string assetName, string assetMapId, string assetId)
        {
            int index = 0;

            foreach (var item in this.Map)
            {
                var asset = assets
                    .Where(a => string.Equals(a.Name, item.AssetName, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (asset != null)
                {
                    foreach (var a in asset.Assets)
                    {
                        if (string.Equals(a.AssetName, assetName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.Equals(item.Id, assetMapId, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(a.Id, assetId, StringComparison.OrdinalIgnoreCase))
                            {
                                return index;
                            }

                            index++;
                        }
                    }
                }
            }

            return -1;
        }
        /// <summary>
        /// Gets the first index of the object in the current configuration
        /// </summary>
        /// <param name="assetName">Asset name</param>
        /// <param name="objectId">Object id</param>
        /// <returns>Returns the first index</returns>
        public int GetObjectInstanceIndex(string assetName, string objectId)
        {
            int index = 0;

            foreach (var item in this.Objects)
            {
                if (string.IsNullOrEmpty(item.AssetName))
                {
                    continue;
                }

                if (string.Equals(item.AssetName, assetName, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(item.Id, objectId, StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }

                    index++;
                }
            }

            return -1;
        }
    }
}