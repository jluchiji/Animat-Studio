﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.Serialization;
using System.IO;
using Animat.UI.Utilities;
using libWyvernzora.Core;
using libWyvernzora.IO;
using libWyvernzora.Utilities;

namespace Animat.UI
{

    /// <summary>
    /// Animat Studio Project instance.
    /// </summary>
    [DataContract(Name = "AnimatProject")]
    public sealed class StudioProject
    {
        #region Life Cycle

        private StudioProject()
        {
            RawAssets = new List<StudioAsset>();
        }

        public static StudioProject CreateProject(String path, String name)
        {
            // Check argument
            if (path == null) throw new ArgumentNullException("path");
            
            // Make sure root directory is there
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            // Set up the project
            var project = new StudioProject { Name = name, Fps = 10 };
            project.ProjectDirectory = path;

            // Set up directories
            if (!Directory.Exists(project.GetAssetDirectory()))
                Directory.CreateDirectory(project.GetAssetDirectory());
            if (!Directory.Exists(project.GetFrameDirectory()))
                Directory.CreateDirectory(project.GetFrameDirectory());
            if (!Directory.Exists(project.GetSequenceDirectory()))
                Directory.CreateDirectory(project.GetSequenceDirectory());
            if (!Directory.Exists(project.GetEventDirectory()))
                Directory.CreateDirectory(project.GetEventDirectory());

            project.SaveProject();

            return project;
        }

        public static StudioProject OpenProject(String path)
        {
            // Deserialize project from file
            StudioProject project = null;
            using (var stream = new StreamEx(path))
            {
                project = JsonUtils.Deserialize<StudioProject>(stream);
            }
            project.ProjectDirectory = Path.GetDirectoryName(path);

            // Sync raw lists to models
            project.assets = new Dictionary<string, StudioAsset>();
            foreach (var p in project.RawAssets)
            {
                p.Initialize(project);
                project.assets.Add(p.Name, p);
            }

            // TODO Load up other stuff


            return project;
        }

        public void SaveProject()
        {
            // Sync models to raw lists
            RawAssets.Clear();
            RawAssets.AddRange(assets.Values);

            // Serialize projec to project file
            using (var stream = new StreamEx(GetProjectFile(), FileMode.Create))
            {
                this.Serialize(stream);
            }
            
            // TODO Save other stuff
        }

        #endregion

        #region Basic Properties

        /// <summary>
        /// Gets or sets the name of the project.
        /// </summary>
        [DataMember(Name = "name")]
        public String Name
        { get; set; }

        /// <summary>
        /// Gets or sets the fps of the animation.
        /// </summary>
        [DataMember(Name = "fps")]
        public UInt32 Fps
        { get; set; }

        #endregion

        #region Directory Management

        // ReSharper disable InconsistentNaming
        [IgnoreDataMember] private const String PROJECT_FILE = "project.bxp";
        [IgnoreDataMember] private const String ASSET_DIR = "assets";
        [IgnoreDataMember] private const String FRAME_DIR = "frames";
        [IgnoreDataMember] private const String SEQ_DIR = "sequences";
        [IgnoreDataMember] private const String EVENT_DIR = "events";
        [IgnoreDataMember] private const String CACHE_DIR = "cache";
        // ReSharper restore InconsistentNaming

        /* Disable Serialization */
        /// <summary>
        /// Path to the folder containing the project.
        /// </summary>
        [IgnoreDataMember]
        public String ProjectDirectory
        { get; set; }

        /// <summary>
        ///     Gets the project file path.
        /// </summary>
        /// <returns></returns>
        public String GetProjectFile()
        {
            return Path.Combine(ProjectDirectory, PROJECT_FILE);
        }

        /// <summary>
        ///     Gets the asset directory of the project.
        /// </summary>
        /// <returns></returns>
        public String GetAssetDirectory()
        {
            return Path.Combine(ProjectDirectory, ASSET_DIR);
        }

        /// <summary>
        ///     Gets the frame directory of the project.
        /// </summary>
        /// <returns></returns>
        public String GetFrameDirectory()
        {
            return Path.Combine(ProjectDirectory, FRAME_DIR);
        }

        /// <summary>
        ///     Gets the sequence directory of the project.
        /// </summary>
        /// <returns></returns>
        public String GetSequenceDirectory()
        {
            return Path.Combine(ProjectDirectory, SEQ_DIR);
        }

        /// <summary>
        ///     Gets the event directory of the project.
        /// </summary>
        /// <returns></returns>
        public String GetEventDirectory()
        {
            return Path.Combine(ProjectDirectory, EVENT_DIR);
        }

        /// <summary>
        /// Gets the cache directory of the project.
        /// </summary>
        /// <returns></returns>
        public String GetCacheDirectory()
        {
            // Get the path
            var path = Path.Combine(ProjectDirectory, CACHE_DIR);
            IOUtilities.EnsureDirectoryExists(path);    // Make sure that there is such a directory.
            return path;
        }

        #endregion

        #region Asset Management

        // Asset store
        [IgnoreDataMember]
        private Dictionary<String, StudioAsset> assets
            = new Dictionary<string, StudioAsset>();

        /// <summary>
        /// Raw representation of the asset list.
        /// In order to retrieve assets, please use Assets property.
        /// </summary>
        [DataMember(Name = "assets")]
        internal List<StudioAsset> RawAssets { get; set; }

        /// <summary>
        /// Gets the enumerable collection of assets associated with this project.
        /// </summary>
        [IgnoreDataMember]
        public IEnumerable<StudioAsset> Assets
        { get { return assets.Values; } }

        /// <summary>
        /// Gets an asset from the asset store.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public StudioAsset GetAsset(String name)
        {
            return assets.ContainsKey(name) ? assets[name] : null;
        }

        /// <summary>
        /// Adds an image to the asset store.
        /// </summary>
        /// <param name="filepath"></param>
        public StudioAsset AddAsset(String filepath)
        {
            // Check arguments
            if (filepath == null) throw new ArgumentNullException("filepath");
            if (!File.Exists(filepath)) throw new FileNotFoundException("Cannot find the asset file to add.");

            // Generate a unique filename
            var filename = DirectIntConv.ToHexString(RandomEx.Instance.NextUInt32(), 8) + Path.GetExtension(filepath);
            while (File.Exists(Path.Combine(GetAssetDirectory(), filename)))
                filename = DirectIntConv.ToHexString(RandomEx.Instance.NextUInt32(), 8) + Path.GetExtension(filepath);

            // Copy the file to the asset folder
            var newPath = Path.Combine(GetAssetDirectory(), filename);
            File.Copy(filepath, newPath);

            // Figure out the best name
            var name = Path.GetFileNameWithoutExtension(filepath);
            if (assets.ContainsKey(name))
            {
                int i = 1;
                while (!assets.ContainsKey(String.Format("{0}({1})", name, i))) i++;
                name = String.Format("{0}({1})", name, i);
            }

            // Create the asset 
            var asset = new StudioAsset(this, filename, name);
            assets.Add(asset.Name, asset);
            

            // Request Update
            StudioCore.Instance.RequestUpdate(UpdateScope.Explorer);

            return asset;
        }

        /// <summary>
        /// Removes an asset from the asset store.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public void RemoveAsset(String name)
        {
            // Get the asset and make sure it exists
            var asset = GetAsset(name);
            if (asset == null) return;

            // Remove it from asset store and thumbnail store
            assets.Remove(name);

            // Delete the asset file
            if (File.Exists(asset.FullPath))
                File.Delete(asset.FullPath);
        }

        /// <summary>
        /// Renames an asset and moves associated files.
        /// </summary>
        /// <param name="name">Name of the asset to rename.</param>
        /// <param name="newName">New name for the asset.</param>
        public void RenameAsset(String name, String newName)
        {
            var asset = GetAsset(name);
            if (asset == null) return;

            // Figure out the best name (in case of duplicates)
            if (assets.ContainsKey(newName))
            {
                int i = 1;
                while (!assets.ContainsKey(String.Format("{0}({1})", newName, i))) i++;
                newName = String.Format("{0}({1})", newName, i);
            }

            asset.Name = newName;
            assets.Remove(name);
            assets.Add(newName, asset);

            SaveProject();
        }
    
        #endregion
    }

}
