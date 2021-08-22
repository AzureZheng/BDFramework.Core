using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;

namespace NugetForUnity
{
    /// <summary>
    /// 解析csproj文件
    /// </summary>
    static public class CsprojFileHelper
    {
        /// <summary>
        /// 解析project
        /// 获取里面的dll和cs
        /// </summary>
        /// <returns></returns>
        static public void ParseCsprojFile(string projPath,
            List<string> blackCspList,
           ref List<string> csList,
           ref List<string> dllList,
           ref List<string> defineList)
        {
            List<string> csprojList = new List<string>();

            #region 解析xml

            XmlDocument xml = new XmlDocument();
            xml.Load(projPath);
            XmlNode ProjectNode = null;
            foreach (XmlNode x in xml.ChildNodes)
            {
                if (x.Name == "Project")
                {
                    ProjectNode = x;
                    break;
                }
            }

            foreach (XmlNode childNode in ProjectNode.ChildNodes)
            {
                if (childNode.Name == "ItemGroup")
                {
                    foreach (XmlNode item in childNode.ChildNodes)
                    {
                        if (item.Name == "Compile") //cs 引用
                        {
                            var csproj = item.Attributes[0].Value;
                            csList.Add(csproj);
                        }
                        else if (item.Name == "Reference") //DLL 引用
                        {
                            var HintPath = item.FirstChild;
                            var dir = HintPath.InnerText.Replace("/", "\\");
                            dllList.Add(dir);
                        }
                        else if (item.Name == "ProjectReference") //工程引用
                        {
                            var csproj = item.Attributes[0].Value;
                            csprojList.Add(csproj);
                        }
                    }
                }
                else if (childNode.Name == "PropertyGroup")
                {
                    foreach (XmlNode item in childNode.ChildNodes)
                    {
                        if (item.Name == "DefineConstants")
                        {
                            var define = item.InnerText;

                            var defines = define.Split(';');

                            defineList.AddRange(defines);
                        }
                    }
                }
            }

            #endregion

            //csproj也加入
            // foreach (var csproj in csprojList)
            // {
            //     //有editor退出
            //     if (csproj.ToLower().Contains("editor") || blackCspList.Contains(csproj))
            //     {
            //         continue;
            //     }
            //
            //     //
            //     var gendll = Application.dataPath + "/Library/ScriptAssemblies/" + csproj.Replace(".csproj", ".dll");
            //     // if (!File.Exists(gendll))
            //     // {
            //     //     Debug.LogError("不存在:" + gendll);
            //     // }
            //
            //     dllList?.Add(gendll);
            //}

            //去重
            csList = csList.Distinct().ToList();
            dllList = dllList.Distinct().ToList();
            defineList = defineList.Distinct().ToList();
        }
    }
}