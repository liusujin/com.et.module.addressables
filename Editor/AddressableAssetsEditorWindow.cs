using System.IO;
using UnityEditor;
using UnityEngine;

namespace ETEditor
{
    public class AddressableAssetsEditorWindow : EditorWindow
    {
        [MenuItem("Tools/打包工具V2.0")]
        public static void ShowDiagnosisWindow()
        {
            EditorWindow.GetWindowWithRect<AddressableAssetsEditorWindow>(new Rect(0, 0, 700, 600), false, "打包工具V2.0");
        }

        private string serverBuildPath = "../Release/{0}/";

        private string[] buildVersionOptions = new string[] { "alpha", "beta", "release", "full version" };
        private int buildVersionIndex = 0;

        private string[] buildPlatformOptions = new string[] { "PC", "Android", "IOS", "MacOS" };
        private int buildPlatformIndex = 0;

        private string buildVersionName = string.Empty;
        private int majorVersion;
        private int minorVersion;
        private int patchVersion;

        private bool isBuildAssets = true;
        private bool isClearStreamingAssetsFolder = true;
        private bool isCopyToStreamingAssetsFolder = false;

        private bool isBuildExecutor = false;
        private string companyName;
        private string productName;

        private string changeLog;

        private Vector2 scrollPosition;

        private void OnEnable()
        {
            this.companyName = PlayerSettings.companyName;
            this.productName = PlayerSettings.productName;

            this.scrollPosition = Vector2.zero;

            string folderPath = $"../Release/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}/";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            if (!File.Exists($"{folderPath}ChangeLog.txt"))
            {
                using (FileStream fs = File.Create($"{folderPath}ChangeLog.txt"))
                {
                    this.changeLog = string.Empty;
                };
            }
            else
            {
                this.changeLog = File.ReadAllText($"../Release/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}/ChangeLog.txt");
            }
        }

        private void OnGUI()
        {
            using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("打包平台：", EditorStyles.boldLabel, GUILayout.Width(100));

                for (int i = 0; i < this.buildPlatformOptions.Length; i++)
                {
                    using (EditorGUILayout.HorizontalScope toggleHorizontal = new EditorGUILayout.HorizontalScope())
                    {
                        if (EditorGUILayout.Toggle(this.buildPlatformIndex == i, EditorStyles.toggle, GUILayout.Width(14)) && this.buildPlatformIndex != i)
                        {
                            this.buildPlatformIndex = i;
                        }
                        EditorGUILayout.LabelField(this.buildPlatformOptions[i], EditorStyles.label, GUILayout.Width(50));
                    }
                }
            }

            using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("打包版本：", EditorStyles.boldLabel, GUILayout.Width(100));

                for (int i = 0; i < this.buildVersionOptions.Length; i++)
                {
                    using (EditorGUILayout.HorizontalScope toggleHorizontal = new EditorGUILayout.HorizontalScope())
                    {
                        if (EditorGUILayout.Toggle(this.buildVersionIndex == i, EditorStyles.toggle, GUILayout.Width(14)) && this.buildVersionIndex != i)
                        {
                            this.buildVersionIndex = i;
                        }
                        EditorGUILayout.LabelField(this.buildVersionOptions[i], EditorStyles.label, GUILayout.Width(80));
                    }
                }
            }

            using (EditorGUILayout.VerticalScope vertical = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("版本号（定义：https://semver.org/lang/zh-CN/）：", EditorStyles.boldLabel, GUILayout.Width(360));

                DirectoryInfo directoryInfo = new DirectoryInfo(string.Format(this.serverBuildPath, UnityEditor.EditorUserBuildSettings.activeBuildTarget));
                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                }
                EditorGUILayout.TextField($"版本文件存储路径：{directoryInfo.FullName}", EditorStyles.label, GUILayout.ExpandWidth(true));

                using (EditorGUILayout.VerticalScope versions = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUILayout.HorizontalScope majorHorizontal = new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("主版本号（当 API 的兼容性变化时，X 需递增）：", EditorStyles.label);
                        this.majorVersion = EditorGUILayout.IntField(this.majorVersion, GUILayout.ExpandWidth(false));
                    }
                    using (EditorGUILayout.HorizontalScope minorHorizontal = new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("次版本号（当增加功能时(不影响 API 的兼容性)，Y 需递增）：", EditorStyles.label);
                        this.minorVersion = EditorGUILayout.IntField(this.minorVersion, GUILayout.ExpandWidth(false));
                    }
                    using (EditorGUILayout.HorizontalScope majorHorizontal = new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("修订号（当做 Bug 修复时(不影响 API 的兼容性)）：", EditorStyles.label);
                        this.patchVersion = EditorGUILayout.IntField(this.patchVersion, GUILayout.ExpandWidth(false));
                    }
                }

                if (this.buildVersionIndex == this.buildVersionOptions.Length - 1)
                {
                    this.buildVersionName = $"{this.majorVersion}.{this.minorVersion}.{this.patchVersion}";
                }
                else
                {
                    this.buildVersionName = $"{this.majorVersion}.{this.minorVersion}.{this.patchVersion}.{this.buildVersionOptions[this.buildVersionIndex]}{System.DateTime.Now.ToString("yyyyMMddHHmmss")}";
                }

                EditorGUILayout.TextField($"待发布版本号：{this.buildVersionName}", EditorStyles.label, GUILayout.ExpandWidth(true));
            }

            using (EditorGUILayout.VerticalScope vertical = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope())
                {
                    this.isBuildAssets = EditorGUILayout.Toggle(this.isBuildAssets, EditorStyles.toggle, GUILayout.Width(14));
                    EditorGUILayout.LabelField("是否构建资源？", EditorStyles.label);
                }
                if (this.isBuildAssets)
                {
                    using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        this.isClearStreamingAssetsFolder = EditorGUILayout.Toggle(this.isClearStreamingAssetsFolder, EditorStyles.toggle, GUILayout.Width(14));
                        EditorGUILayout.LabelField("是否清空StreamingAssets文件夹？", EditorStyles.label);
                    }

                    using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        this.isCopyToStreamingAssetsFolder = EditorGUILayout.Toggle(this.isCopyToStreamingAssetsFolder, EditorStyles.toggle, GUILayout.Width(14));
                        EditorGUILayout.LabelField("是否将资源复制至StreamingAssets文件夹？", EditorStyles.label);
                    }
                }
                else
                {
                    this.isClearStreamingAssetsFolder = false;
                    this.isCopyToStreamingAssetsFolder = false;
                }
            }

            using (EditorGUILayout.VerticalScope vertical = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope())
                {
                    this.isBuildExecutor = EditorGUILayout.Toggle(this.isBuildExecutor, EditorStyles.toggle, GUILayout.Width(14));
                    EditorGUILayout.LabelField("是否打包可执行文件？", EditorStyles.label);
                }
                if (this.isBuildExecutor)
                {
                    using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        this.companyName = EditorGUILayout.TextField("公司名称：", this.companyName, EditorStyles.textField);
                    }

                    using (EditorGUILayout.HorizontalScope horizontal = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        this.productName = EditorGUILayout.TextField("产品名称：", this.productName, EditorStyles.textField);
                    }
                }
            }

            using (EditorGUILayout.VerticalScope vertical = new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(this.position.height - 335)))
            {
                EditorGUILayout.LabelField("发布日志：", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition, GUILayout.ExpandWidth(true), GUILayout.MinHeight(this.position.height - 360 - (this.isBuildExecutor ? 60 : 0))))
                {
                    this.scrollPosition = scroll.scrollPosition;

                    this.changeLog = EditorGUILayout.TextArea(this.changeLog, EditorStyles.textArea);
                }
            }

            if (GUI.Button(new Rect(2, this.position.height - 42, this.position.width - 4, 40), this.isBuildExecutor ? "打包" : "构建资源"))
            {
                PlayerSettings.companyName = this.companyName;
                PlayerSettings.productName = this.productName;

                AddressableAssetsTool.Build(
                    this.buildPlatformOptions[this.buildPlatformIndex],
                    this.buildVersionName,
                    this.isClearStreamingAssetsFolder,
                    this.isCopyToStreamingAssetsFolder,
                    this.isBuildAssets,
                    this.isBuildExecutor);

                File.WriteAllText($"../Release/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}/ChangeLog.txt", this.changeLog);

                AssetDatabase.Refresh();
            }
        }
    }
}