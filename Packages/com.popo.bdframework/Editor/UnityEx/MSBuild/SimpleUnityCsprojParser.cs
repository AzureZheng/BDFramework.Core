using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using BDFramework.Core.Tools;
using UnityEditor;
using UnityEngine;

namespace BDFramework.Editor.UnityEx
{
    /// <summary>
    /// unity csproj解析器
    /// </summary>
    public class SimpleUnityCsprojParser
    {
        private XmlDocument csProjXml;
        private XmlElement CSAndDLLNode;
        private XmlElement ProjectReferenceNode;
        private string[] DefineConstants;

        public SimpleUnityCsprojParser(string projPath)
        {
            csProjXml = new XmlDocument();
            csProjXml.Load(projPath);
            // xml.FirstChild
            XmlNode ProjectNode = null; //xml.ChildNodes. .SelectSingleNode("Project");
            foreach (XmlNode cn in csProjXml.ChildNodes)
            {
                if (cn.Name == "Project")
                {
                    ProjectNode = cn;
                }
            }

            ///寻找节点
            foreach (XmlNode node in ProjectNode.ChildNodes)
            {
                if (node is XmlElement xe)
                {
                    if (xe.Name == "PropertyGroup")
                    {
                        foreach (XmlNode item in xe.ChildNodes)
                        {
                            //宏
                            if (item.Name == "DefineConstants")
                            {
                                this.DefineConstants = item.InnerText.Split(';');
                                break;
                            }
                        }
                    }
                    else if (xe.Name == "ItemGroup")
                    {
                        foreach (XmlNode item in xe.ChildNodes)
                        {
                            if (item.Name == "Compile")
                            {
                                this.CSAndDLLNode = xe;
                                break;
                            }
                            else if (item.Name == "ProjectReference")
                            {
                                this.ProjectReferenceNode = xe;
                                break;
                            }
                        }
                    }


                    if (this.DefineConstants != null && this.CSAndDLLNode != null && this.ProjectReferenceNode != null)
                    {
                        break;
                    }
                }
            }
        }


        /// <summary>
        /// 获取热更Cs
        /// </summary>
        /// <returns></returns>
        public List<string> GetHofixCs()
        {
            var list = new List<string>();
            foreach (XmlNode item in this.CSAndDLLNode.ChildNodes)
            {
                if (item.Name == "Compile") //cs 引用
                {
                    var csproj = item.Attributes["Include"];
                    list.Add(csproj.Value);
                }
            }

            return list;
        }


        /// <summary>
        /// 添加cs文件
        /// </summary>
        /// <param name="path"></param>
        public void AddCSFile(string path)
        {
            var csfileNode = csProjXml.CreateElement("Compile");
            csfileNode.SetAttribute("Include", path);
            CSAndDLLNode.AppendChild(csfileNode);
        }

        /// <summary>
        /// 添加dll
        /// </summary>
        /// <param name="path"></param>
        public void AddDll(string path)
        {
            // ProjectReferenceNode.ChildNodes.
        }

        /// <summary>
        /// 添加工程应用
        /// </summary>
        /// <param name="path"></param>
        public void AddProjectReference(string path)
        {
        }


        /// <summary>
        /// 保存csproj配置文件
        /// </summary>
        public void SaveCsproj(string path)
        {
            this.csProjXml.Save(path);
            Debug.Log("【CSProj】保存完成!");
        }


        /// <summary>
        /// 拷贝cs
        /// </summary>
        public void CopyCsToBDWorkSpace(string copyToPath)
        {
            foreach (XmlNode node in CSAndDLLNode.ChildNodes)
            {
                if (node.Name == "Compile")
                {
                    var element = node as XmlElement;
                    var csPath = element.Attributes["Include"].Value;
                    if (csPath.Contains("@hotfix"))
                    {
                        var copyto = IPath.Combine(copyToPath, csPath);
                        var dir = Path.GetDirectoryName(copyto);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        //拷贝
                        File.Copy(csPath, copyto, true);
                    }
                }
            }
        }
        /// <summary>
        /// 移动cs
        /// </summary>
        public void MoveCsToBDWorkSpace(string moveToPath)
        {
            foreach (XmlNode node in CSAndDLLNode.ChildNodes)
            {
                if (node.Name == "Compile")
                {
                    var element = node as XmlElement;
                    var csPath = element.Attributes["Include"].Value;
                    if (csPath.Contains("@hotfix"))
                    {
                        var moveto = IPath.Combine(moveToPath, csPath);
                        var dir = Path.GetDirectoryName(moveto);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        //移动
                        File.Move(csPath, moveto);
                    }
                }
            }
        }

        /// <summary>
        /// 用当前的csproj生成BDWorkSpade的csproj、sln
        /// </summary>
        public void GenBDWorkSpaceHotfixCsprojSln(string workspacePath)
        {
            var removeNodeList = new List<XmlNode>();
            foreach (XmlNode node in CSAndDLLNode.ChildNodes)
            {
                if (node.Name == "Compile")
                {
                    removeNodeList.Add(node);
                }
                else if (node.Name == "None")
                {
                    removeNodeList.Add(node);
                }
            }
            //移除
            foreach (var rn in removeNodeList)
            {
                CSAndDLLNode.RemoveChild(rn);
            }
            //获取所有CSFile
            var csfiles = Directory.GetDirectories(BDApplication.BDWorkSpaceHotFixCode);
            foreach (var cs in csfiles)
            {
              this.AddCSFile(cs);  
            }
            //1.修改projectGuid
            //2.修改宏,
            //3.修改dll输出目录
            //5.添加Assembly-CSharp的工程引用
            //6.新建HotfixCode.sln
        }
    }
}