﻿using System.IO;
using BDFramework.Core.Tools;
using Microsoft.Build.Construction;
using UnityEngine;

namespace BDFramework.Editor.WorkFollow
{
    /// <summary>
    /// 热更代码工作流
    /// </summary>
    static public class HotfixCodeWorkFollow
    {
        /// <summary>
        /// 热更代码发生改变
        /// </summary>
        static public void OnCodeChanged()
        {
            //获取最近修改的代码
            var codes = BDEditorApplication.GetLeastHotfixCodes();
            if (codes != null && codes.Length > 0) //修改过Hotfix
            {
                if (BDEditorApplication.BdFrameEditorSetting.WorkFollow.IsNeedAutoBuildDll())
                {
                    EditorWindow_ScriptBuildDll.RoslynBuild(Application.streamingAssetsPath, Application.platform,
                        ScriptBuildTools.BuildMode.Debug, false);
                    Debug.Log("自动编译Hotfix.dll成功!");
                }
                else if (BDEditorApplication.BdFrameEditorSetting.WorkFollow.IsHotfixCodeOutofUnityAssets())
                {
                    MoveCodeToBDWorkSpace(codes);
                }
            }
        }


        /// <summary>
        /// 迁移代码到BDWorkSpace
        /// </summary>
        static public void MoveCodeToBDWorkSpace(string[] hotfixCodes)
        {
            var targetRoot = BDApplication.BDWorkSpace + "/HotfixCode";
            
            foreach (var codePath in hotfixCodes)
            {
                var targetpath = Path.Combine(targetRoot, codePath);
                if (codePath.StartsWith("Assets")) //移动
                {
                    
                    FileHelper.Copy(codePath, targetpath,true);
                }
                else if (codePath.StartsWith("Package")) //拷贝&&覆盖
                {
                    FileHelper.Copy(codePath, targetpath, true);
                }
            }

            AddCSFileToHotfix();
        }
        
        /// <summary>
        /// 添加热更
        /// </summary>
        /// <param name="file"></param>
        static void AddCSFileToHotfix()
        {
            var sln = BDApplication.ProjectRoot+ "/Assembly-CSharp.csproj";
           // var slution = SolutionFile.Parse(sln[0]);
            var proj = ProjectRootElement.Open(sln);

            foreach (var itemGroup in proj.ItemGroups)
            {
                foreach (var itemElement in itemGroup.Items)
                {
                    Debug.Log(itemElement.Include);
                }
            }
            // foreach (var project in slution.ProjectsInOrder)
            // {
            //     project.
            // }
            //
        }
    }
}