using ETModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace ETEditor
{
    [InitializeOnLoad]
    public class AddressableAssetsTool
    {
        #region 监听文件变化
        private static FileSystemWatcher watcher;
        private static bool watcherHasTriggered;

        static AddressableAssetsTool()
        {
            watcher?.Dispose();
            watcher = null;

            string path = $"{UnityEngine.Application.dataPath}/Addressables/";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            watcher = new FileSystemWatcher();
            watcher.Path = path;
            watcher.IncludeSubdirectories = true;

            watcher.Changed += OnChangedEventHandle;
            watcher.Created += OnCreatedEventHandle;
            watcher.Deleted += OnDeletedEventHandle;
            watcher.Renamed += OnRenamedEventHandle;

            watcher.EnableRaisingEvents = true;

            EditorApplication.update -= OnEditorApplicationUpdate;
            EditorApplication.update += OnEditorApplicationUpdate;
        }

        private static void OnRenamedEventHandle(object sender, RenamedEventArgs e)
        {
            watcherHasTriggered = true;
        }

        private static void OnDeletedEventHandle(object sender, FileSystemEventArgs e)
        {
            watcherHasTriggered = true;
        }

        private static void OnCreatedEventHandle(object sender, FileSystemEventArgs e)
        {
            watcherHasTriggered = true;
        }

        private static void OnChangedEventHandle(object sender, FileSystemEventArgs e)
        {
            watcherHasTriggered = true;
        }

        private static void OnEditorApplicationUpdate()
        {
            if (watcherHasTriggered)
            {
                watcherHasTriggered = false;
                Reimport();
            }
        }
        #endregion

        private static void Reimport()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);                
            }
            AddressableAssetSettings settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>($"{AddressableAssetSettingsDefaultObject.kDefaultConfigFolder}/{AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName}.asset");
            //if (settings.profileSettings.GetVariableNames().Contains(kStreamingLoadPath))
            //{
            //    settings.profileSettings.SetValue(settings.profileSettings.GetProfileId("Default"), kStreamingLoadPath, "{UnityEngine.Application.streamingAssetsPath}/[BuildTarget]");
            //}
            //else
            //{
            //    settings.profileSettings.CreateValue(kStreamingLoadPath, "{UnityEngine.Application.streamingAssetsPath}/[BuildTarget]");
            //}

            if (bundleNamingPropertyInfo == null)
            {
                bundleNamingPropertyInfo = typeof(BundledAssetGroupSchema).GetProperty("BundleNaming", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            //更新Group模板的发布路径和加载路径
            UpdateGroupTemplateBuildAndLoadPath(settings, $"{settings.GroupTemplateFolder}/Packed Assets.asset");
            UpdateGroupBuildAndLoadPath(settings, $"{settings.GroupFolder}/Default Local Group.asset");

            //清除旧有资源
            CleanGroup(settings);

            //自动生成Group
            List<AddressableAssetGroupSchema> schemas = AssetDatabase.LoadAssetAtPath<AddressableAssetGroupTemplate>($"{settings.GroupTemplateFolder}/Packed Assets.asset").SchemaObjects;
            string[] directories = Directory.GetDirectories($"{UnityEngine.Application.dataPath}/Addressables/");
            //string remoteBuildPath = $"./{settings.profileSettings.GetValueByName(settings.profileSettings.GetProfileId("Default"), AddressableAssetSettings.kRemoteBuildPath).Replace("[BuildTarget]", UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString())}";
            //string localBuildPath = $"./{settings.profileSettings.GetValueByName(settings.profileSettings.GetProfileId("Default"), AddressableAssetSettings.kLocalBuildPath).Replace("[UnityEngine.AddressableAssets.Addressables.BuildPath]", UnityEngine.AddressableAssets.Addressables.BuildPath).Replace("[BuildTarget]", UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString())}";
            string buildPath = $"./{UnityEngine.AddressableAssets.Addressables.BuildPath}";
            foreach (string folderPath in directories)
            {
                if (folderPath.Substring(folderPath.LastIndexOf('/') + 1).StartsWith("~"))
                {
                    continue;
                }
                BuildGroup(folderPath, settings, schemas);
            }

            //生成资源地址
            GenerateAddress();
        }

        private const string relativeDirPrefix = "../Release";

        private static PropertyInfo bundleNamingPropertyInfo;

        public static void Build(string target, string versionName, bool isClearStreamingAssetsFolder, bool isCopyToStreamingAssetsFolder, bool isBuildAssets, bool isBuildExecutor)
        {
            Reimport();

            string buildPath = $"./{UnityEngine.AddressableAssets.Addressables.BuildPath}";
            //打包资源
            if (isBuildAssets)
            {
                AddressableAssetSettings.CleanPlayerContent();
                AddressableAssetSettings.BuildPlayerContent();

                if (!Directory.Exists(UnityEngine.Application.streamingAssetsPath))
                {
                    Directory.CreateDirectory(UnityEngine.Application.streamingAssetsPath);
                }

                //删除StreamingAssets文件夹
                if (isClearStreamingAssetsFolder)
                {
                    FileHelper.CleanDirectory("Assets/StreamingAssets/");
                }

                //复制打包资源到服务器资源文件夹
                FileHelper.CopyDirectory(buildPath.Substring(0, buildPath.IndexOf("aa")), $"../Release/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}/{versionName}/");

                //生成版本控制文件
                GenerateVersionInfo(buildPath.Substring(0, buildPath.IndexOf("aa")), $"../Release/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}/", versionName);

                //复制资源到StreamingAssets文件夹
                if (isCopyToStreamingAssetsFolder)
                {
                    //复制打包资源文件到StreamingAsset文件夹
                    FileHelper.CopyDirectory(buildPath.Substring(0, buildPath.IndexOf("aa")), UnityEngine.Application.streamingAssetsPath);
                    File.Copy($"../Release/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}/Version.txt", $"{UnityEngine.Application.streamingAssetsPath}/Version.txt", true);
                }

                AssetDatabase.Refresh();
            }

            if (isBuildExecutor)
            {
                string[] levels = {
                    "Assets/Scenes/Application.unity",
                };
                DirectoryInfo directoryInfo = new System.IO.DirectoryInfo($"{relativeDirPrefix}/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}/发布文件/{PlayerSettings.productName}_{UnityEngine.SystemInfo.deviceName}_{System.DateTime.Now.ToString("yyyyMMddmmHHss")}/");
                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                }

                Log.Info("开始打包可执行文件...");
                BuildPipeline.BuildPlayer(levels, $"{directoryInfo.FullName}{PlayerSettings.productName}.exe", EditorUserBuildSettings.activeBuildTarget, BuildOptions.None);
                Log.Info("可执行文件打包完成.");
                AssetDatabase.Refresh();

                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "explorer";
                process.StartInfo.Arguments = @"/e /root," + directoryInfo.FullName;
                process.Start();
            }

            Log.Info("完成");
        }

        private static void CleanGroup(AddressableAssetSettings settings)
        {
            foreach (AddressableAssetGroup group in settings.groups.ToArray())
            {
                if (group.name == "Built In Data" || group.name == "Default Local Group")
                {
                    continue;
                }
                settings.RemoveGroup(group);
            }
        }

        private static void UpdateGroupTemplateBuildAndLoadPath(AddressableAssetSettings settings, string path)
        {
            List<AddressableAssetGroupSchema> schemas = AssetDatabase.LoadAssetAtPath<AddressableAssetGroupTemplate>(path).SchemaObjects;
            BundledAssetGroupSchema bundledAssetGroupSchema = schemas.Find(p => p.GetType() == typeof(BundledAssetGroupSchema)) as BundledAssetGroupSchema;
            if (bundledAssetGroupSchema != null)
            {
                bundledAssetGroupSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundledAssetGroupSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);

                bundleNamingPropertyInfo.SetValue(bundledAssetGroupSchema, 1);
            }
        }

        private static void UpdateGroupBuildAndLoadPath(AddressableAssetSettings settings, string path)
        {
            List<AddressableAssetGroupSchema> schemas = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(path).Schemas;
            BundledAssetGroupSchema bundledAssetGroupSchema = schemas.Find(p => p.GetType() == typeof(BundledAssetGroupSchema)) as BundledAssetGroupSchema;
            if (bundledAssetGroupSchema != null)
            {
                bundledAssetGroupSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundledAssetGroupSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);

                bundleNamingPropertyInfo.SetValue(bundledAssetGroupSchema, 1);
            }
        }

        private static void BuildGroup(string groupFolder, AddressableAssetSettings settings, List<AddressableAssetGroupSchema> schemas)
        {
            List<string> assetPaths = Directory.EnumerateFiles(groupFolder, "*.*", SearchOption.AllDirectories)
                .Where(p => Path.GetExtension(p) != ".meta")
                .Select(p => p.Substring(UnityEngine.Application.dataPath.Length - 6))
                .ToList();
            if (assetPaths.Count < 1)
            {
                return;
            }
            string groupName = groupFolder.Substring(groupFolder.LastIndexOf('/') + 1);
            if (groupName.StartsWith("~") || groupName == "Resources")
            {
                return;
            }
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group != null)
            {
                settings.RemoveGroup(group);
            }
            group = settings.CreateGroup(groupName, false, false, true, schemas);
            foreach (string path in assetPaths)
            {
                string address = Path.GetFileNameWithoutExtension(path);
                if (address.StartsWith("~"))
                {
                    continue;
                }
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            }

            if (groupName == "Default Local Group")
            {
                settings.DefaultGroup = group;
            }
        }

        private static void GenerateAddress()
        {
            AddressTool.GenerateAddress();
        }

        private static void GenerateVersionInfo(string srcFolder, string versionFileFolder, string versionName)
        {
            VersionConfig versionProto = new VersionConfig();
            GenerateVersionProto(srcFolder, versionProto, "");
            versionProto.VersionDescription = versionName;

            using (FileStream fileStream = new FileStream($"{versionFileFolder}/Version.txt", FileMode.Create))
            {
                byte[] bytes = JsonHelper.ToJson(versionProto).ToByteArray();
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        private static void GenerateVersionProto(string dir, VersionConfig versionProto, string relativePath)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                if (System.IO.Path.GetFileNameWithoutExtension(file) == "Version")
                {
                    continue;
                }
                string md5 = MD5Helper.FileMD5(file);
                FileInfo fi = new FileInfo(file);
                long size = fi.Length;
                string filePath = relativePath == "" ? fi.Name : $"{relativePath}/{fi.Name}";

                versionProto.FileInfoDict.Add(filePath, new FileVersionInfo
                {
                    File = filePath,
                    MD5 = md5,
                    Size = size,
                });
            }

            foreach (string directory in Directory.GetDirectories(dir))
            {
                DirectoryInfo dinfo = new DirectoryInfo(directory);
                string rel = relativePath == "" ? dinfo.Name : $"{relativePath}/{dinfo.Name}";
                GenerateVersionProto($"{dir}/{dinfo.Name}", versionProto, rel);
            }
        }
    }
}