using System;
using System.Collections.Generic;
using System.IO;

namespace ETModel
{
    [ObjectSystem]
    public class AddressablesComponentAwakeSystem : AwakeSystem<AddressablesComponent>
    {
        public override void Awake(AddressablesComponent self)
        {
            self.Awake();
        }
    }

    public class AddressablesComponent : Component
    {
        /// <summary>
        /// 远程版本文件
        /// </summary>
        private VersionConfig remoteVersionConfig;

        /// <summary>
        /// 已下载资源
        /// </summary>
        public HashSet<string> DownloadedBundles;
        /// <summary>
        /// 待下载资源
        /// </summary>
        public Queue<string> Bundles;
        /// <summary>
        /// 总待下载资源数量
        /// </summary>
        public long BundlesCount;

        /// <summary>
        /// 总已下载字节
        /// </summary>
        public long AlreadyDownloadedBytes;
        /// <summary>
        /// 总待下载字节
        /// </summary>
        public long TotalSize;

        /// <summary>
        /// 当前下载资源
        /// </summary>
        public string DownloadingBundle;
        /// <summary>
        /// 当前下载资源已下载字节
        /// </summary>
        public long DownloadingAlreadyDownloadedZise;
        /// <summary>
        /// 当前下载资源总待下载字节
        /// </summary>
        public long DownloadingTotalSize;

        public UnityWebRequestAsync WebRequest;

        /// <summary>
        /// 当前下载资源下载进度
        /// </summary>
        public int DownloadingProgress
        {
            get
            {
                if (this.DownloadingTotalSize == 0)
                {
                    return 0;
                }

                this.DownloadingAlreadyDownloadedZise = 0;
                if (this.WebRequest != null)
                {
                    this.DownloadingAlreadyDownloadedZise = (long)this.WebRequest.Request.downloadedBytes;
                }
                return (int)(this.DownloadingAlreadyDownloadedZise * 100f / this.DownloadingTotalSize);
            }
        }

        /// <summary>
        /// 总下载进度
        /// </summary>
        public int Progress
        {
            get
            {
                if (this.TotalSize == 0)
                {
                    return 0;
                }

                this.AlreadyDownloadedBytes = 0;
                foreach (string downloadedBundle in this.DownloadedBundles)
                {
                    long size = this.remoteVersionConfig.FileInfoDict[downloadedBundle].Size;
                    this.AlreadyDownloadedBytes += size;
                }
                this.DownloadingAlreadyDownloadedZise = 0;
                if (this.WebRequest != null)
                {
                    this.DownloadingAlreadyDownloadedZise = (long)this.WebRequest.Request.downloadedBytes;
                }
                this.AlreadyDownloadedBytes += this.DownloadingAlreadyDownloadedZise;
                return (int)(this.AlreadyDownloadedBytes * 100f / this.TotalSize);
            }
        }

        public void Awake()
        {
            this.Bundles = new Queue<string>();
            this.DownloadedBundles = new HashSet<string>();
            this.DownloadingBundle = string.Empty;
        }

        public async ETTask StartAsync()
        {
            // 获取远程的Version.txt
            string versionUrl = "";
            try
            {
                using (UnityWebRequestAsync webRequestAsync = ComponentFactory.Create<UnityWebRequestAsync>())
                {
                    //await webRequestAsync.DownloadAsync(GlobalConfigComponent.Instance.GlobalProto.GetUrl() + "Version.txt");                    
                    //remoteVersionConfig = JsonHelper.FromJson<VersionConfig>(webRequestAsync.Request.downloadHandler.text);
                    remoteVersionConfig = JsonHelper.FromJson<VersionConfig>(File.ReadAllText(Path.Combine(PathHelper.AppResPath4Web, "Version.txt")));
                }
            }
            catch (Exception e)
            {
                throw new Exception($"url: {versionUrl}", e);
            }

            // 获取streaming目录的Version.txt
            VersionConfig streamingVersionConfig;
            string versionPath = Path.Combine(PathHelper.AppResPath4Web, "Version.txt");
            if (File.Exists(versionPath))
            {
                using (UnityWebRequestAsync request = ComponentFactory.Create<UnityWebRequestAsync>())
                {
                    //await request.DownloadAsync(versionPath);
                    //streamingVersionConfig = JsonHelper.FromJson<VersionConfig>(request.Request.downloadHandler.text);
                    streamingVersionConfig = JsonHelper.FromJson<VersionConfig>(File.ReadAllText(Path.Combine(PathHelper.AppResPath4Web, "Version.txt")));
                }
            }
            else
            {
                streamingVersionConfig = new VersionConfig();
            }

            // 删掉远程不存在的文件
            DirectoryInfo directoryInfo = new DirectoryInfo(PathHelper.AppHotfixResPath);
            if (directoryInfo.Exists)
            {
                FileInfo[] fileInfos = directoryInfo.GetFiles();
                int directoryFolderLength = directoryInfo.FullName.Length + 1;
                foreach (FileInfo fileInfo in fileInfos)
                {
                    if (remoteVersionConfig.FileInfoDict.ContainsKey(fileInfo.FullName.Substring(directoryFolderLength)))
                    {
                        continue;
                    }

                    if (fileInfo.Name == "Version.txt")
                    {
                        continue;
                    }

                    fileInfo.Delete();
                }
            }
            else
            {
                directoryInfo.Create();
            }

            // 对比MD5
            foreach (FileVersionInfo fileVersionInfo in remoteVersionConfig.FileInfoDict.Values)
            {
                // 对比md5
                string localFileMD5 = GetBundleMD5(streamingVersionConfig, fileVersionInfo.File);
                if (fileVersionInfo.MD5 == localFileMD5)
                {
                    continue;
                }
                this.Bundles.Enqueue(fileVersionInfo.File);
                this.TotalSize += fileVersionInfo.Size;
            }
            this.BundlesCount = this.Bundles.Count;
        }

        public static string GetBundleMD5(VersionConfig streamingVersionConfig, string bundleName)
        {
            string path = Path.Combine(PathHelper.AppHotfixResPath, $"{bundleName}");
            if (File.Exists(path))
            {
                return MD5Helper.FileMD5(path);
            }

            if (streamingVersionConfig.FileInfoDict.ContainsKey(bundleName))
            {
                return streamingVersionConfig.FileInfoDict[bundleName].MD5;
            }

            return "";
        }

        public async ETTask DownloadAsync()
        {
            if (this.Bundles.Count == 0 && this.DownloadingBundle == "")
            {
                return;
            }

            try
            {
                while (true)
                {
                    if (this.Bundles.Count == 0)
                    {
                        using (FileStream fileStream = new FileStream(Path.Combine(PathHelper.AppResPath4Web, "Version.txt"), FileMode.OpenOrCreate))
                        {
                            byte[] bytes = JsonHelper.ToJson(remoteVersionConfig).ToByteArray();
                            fileStream.Write(bytes, 0, bytes.Length);
                        }
                        break;
                    }

                    this.DownloadingBundle = this.Bundles.Dequeue();
                    this.DownloadingTotalSize = this.remoteVersionConfig.FileInfoDict[this.DownloadingBundle].Size;

                    while (true)
                    {
                        try
                        {
                            using (this.WebRequest = ComponentFactory.Create<UnityWebRequestAsync>())
                            {
                                await this.WebRequest.DownloadAsync(GlobalConfigComponent.Instance.GlobalProto.GetUrl() + $"/{this.remoteVersionConfig.VersionDescription}/{this.DownloadingBundle}");
                                byte[] data = this.WebRequest.Request.downloadHandler.data;

                                string filePath = Path.Combine(PathHelper.AppHotfixResPath + "/", this.DownloadingBundle);
                                string directoryPath = Path.GetDirectoryName(filePath);
                                if (!Directory.Exists(directoryPath))
                                {
                                    Directory.CreateDirectory(directoryPath);
                                }
                                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                                {
                                    fs.Write(data, 0, data.Length);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error($"download bundle error: {this.DownloadingBundle}\n{e}");
                            continue;
                        }

                        break;
                    }

                    this.DownloadedBundles.Add(this.DownloadingBundle);
                    this.DownloadingBundle = "";
                    this.WebRequest = null;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}