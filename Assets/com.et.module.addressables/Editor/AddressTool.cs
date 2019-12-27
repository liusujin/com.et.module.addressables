using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ETEditor
{
    public class AddressTool
    {
        public static void GenerateAddress()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("namespace ETModel");
            sb.AppendLine("{");
            sb.AppendLine("\tpublic sealed class Address");
            sb.AppendLine("\t{");

            int indent = 2;
            string baseFolderPath = UnityEngine.Application.dataPath + "/Addressables/";

            IEnumerable<string> directories = Directory.EnumerateDirectories(baseFolderPath);
            foreach (string directory in directories)
            {
                string name = directory.Substring(directory.LastIndexOf("/") + 1);
                if (name == "Resources" || name.StartsWith("~"))
                {
                    continue;
                }

                GenerateAddress(UnityEngine.Application.dataPath.Length - 6, indent, directory, p => p.StartsWith("~"), sb);
            }

            sb.AppendLine("\t}");
            sb.AppendLine("}");

            string folderPath = $"{UnityEngine.Application.dataPath}/Model/Addressables/";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string filePath = $"{folderPath}Address.cs";
            if (!File.Exists(filePath))
            {
                using (StreamWriter sw = File.CreateText(filePath))
                {
                    sw.Write(sb.ToString());
                    sw.Dispose();
                }
            }
            else
            {
                File.WriteAllText(filePath, sb.ToString());
            }

            UnityEditor.AssetDatabase.Refresh();
        }

        /// <summary>
        /// 递归生成资源地址
        /// </summary>
        /// <param name="prefixPathLength">路径前缀长度</param>
        /// <param name="indent">代码缩进</param>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="predicate">文件夹过滤条件</param>
        /// <param name="sb">文本构建器</param>
        private static void GenerateAddress(int prefixPathLength, int indent, string folderPath, Predicate<string> predicate, StringBuilder sb)
        {
            folderPath = folderPath.Replace("\\", "/");
            string classIndent = string.Join("\t", Enumerable.Range(0, indent).Select(p => "\t"));
            string fieldIndent = string.Join("\t", Enumerable.Range(0, indent + 1).Select(p => "\t"));
            string folderName = folderPath.Substring(folderPath.LastIndexOf("/") + 1);

            sb.AppendLine();
            sb.AppendLine($"{classIndent}public sealed class {NormalizedName(folderName)}");
            sb.AppendLine($"{classIndent}{{");
            IEnumerable<IGrouping<string, string>> files = Directory.GetFiles(folderPath)
                .Where(p => Path.GetExtension(p) != ".meta")
                .GroupBy(p => Path.GetFileNameWithoutExtension(p));
            foreach (IGrouping<string, string> group in files)
            {
                if (group.Key.StartsWith("~"))
                {
                    continue;
                }
                if (group.Count() == 1)
                {
                    sb.AppendLine($"{fieldIndent}public const string {NormalizedName(group.Key)} = \"{group.ElementAt(0).Substring(prefixPathLength).Replace("\\", "/")}\";");
                }
                else
                {
                    for (int i = 0; i < group.Count(); ++i)
                    {
                        sb.AppendLine($"{fieldIndent}public const string {NormalizedName(group.Key)}_{Path.GetExtension(group.ElementAt(i)).Substring(1)} = \"{group.ElementAt(i).Substring(prefixPathLength).Replace("\\", "/")}\";");
                    }
                }
            }

            indent++;
            IEnumerable<string> directories = Directory.EnumerateDirectories(folderPath);
            foreach (string directory in directories)
            {
                string name = directory.Replace("\\", "/").Substring(directory.LastIndexOf("/") + 1);
                if (predicate(name))
                {
                    continue;
                }

                GenerateAddress(prefixPathLength, indent, directory, p => p.StartsWith("~"), sb);
            }


            sb.AppendLine($"{classIndent}}}");
        }

        private static string NormalizedName(string input)
        {
            return Regex.Replace(input.Replace(" ", string.Empty), string.Join(string.Empty, System.IO.Path.GetInvalidFileNameChars()), string.Empty);
        }
    }
}