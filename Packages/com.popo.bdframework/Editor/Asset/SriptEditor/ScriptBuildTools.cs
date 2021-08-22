using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using BDFramework;
using BDFramework.AssetHelper;
using Debug = UnityEngine.Debug;
using BDFramework.Core.Tools;
using BDFramework.Tool;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Unity.CodeEditor;
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

#endif
public class ScriptBuildTools
{
    public enum BuildMode
    {
        Release,
        Debug,
    }


    private static Dictionary<int, string> csFilesMap;
    private static string DLLPATH { get; set; } = ScriptLoder.DLLPATH; // "Hotfix/hotfix.dll";


    private static bool IsShowTips;

    /// <summary>
    /// 宏
    /// </summary>
    private static List<string> defineList;

    /// <summary>
    /// 编译DLL
    /// </summary>
    static public void BuildDll(string outPath, RuntimePlatform platform, BuildMode mode, bool isShowTips = true)
    {
        IsShowTips = isShowTips;

        if (IsShowTips)
        {
            EditorUtility.DisplayProgressBar("编译服务", "准备编译环境...", 0.1f);
        }

        //生成CSProj
        EditorApplication.ExecuteMenuItem("Assets/Open C# Project");

        
        //准备输出环境
        var _outPath = Path.Combine(outPath, BDApplication.GetPlatformPath(platform));
        try
        {
            var path = _outPath + "/Hotfix";
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
            if (IsShowTips)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("提示", "请手动删除hotfix文件后重试!", "OK");
            }

            return;
        }

        if (IsShowTips)
        {
            EditorUtility.DisplayProgressBar("编译服务", "开始处理脚本", 0.2f);
        }

        #region CS DLL引用搜集处理

        List<string> dllFileList = new List<string>();
        List<string> csFileList = new List<string>();
        //所有宏
        defineList = new List<string>();

        var gameLogicCsproj =BDApplication.ProjectRoot + "/Assembly-CSharp.csproj"; //游戏逻辑的代码
        var frameworkCsproj = BDApplication.ProjectRoot + "/BDFramework.Core.csproj"; //框架部分的代码
        
        CsprojFileHelper.ParseCsprojFile(gameLogicCsproj, new List<string>() {"BDFramework.Core.csproj"}, ref csFileList, ref dllFileList,ref defineList);
        CsprojFileHelper.ParseCsprojFile(frameworkCsproj, new List<string>(), ref csFileList, ref dllFileList,ref defineList);


        //宏解析
        //移除editor相关宏
        for (int i = defineList.Count - 1; i >= 0; i--)
        {
            var symbol = defineList[i];
            if (symbol.Contains("UNITY_EDITOR"))
            {
                defineList.RemoveAt(i);
            }
        }

        //剔除不存的dll
        for (int i = dllFileList.Count - 1; i >= 0; i--)
        {
            var dll = dllFileList[i];
            if (!File.Exists(dll))
            {
                dllFileList.RemoveAt(i);
                Debug.Log("剔除:" + dll);
            }
        }
        #endregion


        // 热更代码 = 框架部分@hotfix  +  游戏逻辑部分@hotfix
        var baseCs = csFileList.FindAll(f => !f.Contains("@hotfix") && f.EndsWith(".cs")); //筛选cs
        //不用ILR binding进行编译base.dll,因为binding本身会因为@hotfix调整容易报错
        baseCs = baseCs.Where((cs) =>
                (!cs.Contains("\\ILRuntime\\Binding\\Analysis\\") && !cs.Contains("/ILRuntime/Binding/Analysis/")) ||
                cs.EndsWith("CLRBindings.cs"))
            .ToList();
        //
        var hotfixCs = csFileList.FindAll(f => f.Contains("@hotfix") && f.EndsWith(".cs"));
        var outHotfixPath = Path.Combine(_outPath, DLLPATH);




        if (mode == BuildMode.Release)
        {
            Build(baseCs, hotfixCs, dllFileList, outHotfixPath);
        }
        else if (mode == BuildMode.Debug)
        {
            Build(baseCs, hotfixCs, dllFileList, outHotfixPath, true);
        }

        AssetHelper.GenPackageBuildInfo(outPath, platform);
    }

    /// <summary>
    /// 编译
    /// </summary>
    /// <param name="tempCodePath"></param>
    /// <param name="outBaseDllPath"></param>
    /// <param name="outHotfixDllPath"></param>
    static public void Build(List<string> baseCs,
        List<string> hotfixCS,
        List<string> dllFiles,
        string outHotfixDllPath,
        bool isdebug = false)
    {
        var baseDll =
            outHotfixDllPath.Replace("hotfix.dll",
                "Assembly-CSharp.dll"); //这里早期叫base.dll，后因为mono执行依赖Assembly-CSharp.dll
        //开始执行
        if (IsShowTips)
        {
            EditorUtility.DisplayProgressBar("编译服务", "[1/2]开始编译base.dll...", 0.5f);
        }

        try
        {
            //使用宏编译
            BuildByRoslyn(dllFiles.ToArray(), baseCs.ToArray(), baseDll, false, true);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
            EditorUtility.ClearProgressBar();
            return;
        }

        if (IsShowTips)
        {
            EditorUtility.DisplayProgressBar("编译服务", "[2/2]开始编译hotfix.dll...", 0.7f);
        }

        //将base.dll加入
        //var mainDll = BApplication.ProjectRoot + "/Library/ScriptAssemblies/Assembly-CSharp.dll";
        if (!dllFiles.Contains(baseDll))
        {
            dllFiles.Add(baseDll);
        }

        //build
        try
        {
            //这里编译 不能使用宏
            BuildByRoslyn(dllFiles.ToArray(), hotfixCS.ToArray(), outHotfixDllPath, isdebug, false);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
            EditorUtility.ClearProgressBar();
            return;
        }

        if (IsShowTips)
        {
            EditorUtility.DisplayProgressBar("编译服务", "清理临时文件", 0.9f);
        }
        File.Delete(baseDll);
        if (IsShowTips)
        {
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.Refresh();
    }


    /// <summary>
    /// 编译dll
    /// </summary>
    /// <param name="rootpaths"></param>
    /// <param name="output"></param>
    static public bool BuildByRoslyn(string[] dlls,
        string[] codefiles,
        string output,
        bool isdebug = false,
        bool isUseDefine = false)
    {
        if (Application.platform == RuntimePlatform.OSXEditor)
        {
            for (int i = 0; i < dlls.Length; i++)
            {
                dlls[i] = dlls[i].Replace("\\", "/");
            }

            for (int i = 0; i < codefiles.Length; i++)
            {
                codefiles[i] = codefiles[i].Replace("\\", "/");
            }

            output = output.Replace("\\", "/");
        }

        //添加语法树
        var Symbols = defineList;

        List<Microsoft.CodeAnalysis.SyntaxTree> codes = new List<Microsoft.CodeAnalysis.SyntaxTree>();
        CSharpParseOptions opa = null;
        if (isUseDefine)
        {
            opa = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: Symbols);
        }
        else
        {
            opa = new CSharpParseOptions(LanguageVersion.Latest);
        }

        foreach (var cs in codefiles)
        {
            //判断文件是否存在
            if (!File.Exists(cs))
                continue;
            //
            var content = File.ReadAllText(cs);
            var syntaxTree = CSharpSyntaxTree.ParseText(content, opa, cs, Encoding.UTF8);
            codes.Add(syntaxTree);
        }

        //添加dll
        List<MetadataReference> assemblies = new List<MetadataReference>();
        foreach (var dll in dlls)
        {
            var metaref = MetadataReference.CreateFromFile(dll);
            if (metaref != null)
            {
                assemblies.Add(metaref);
            }
        }

        //创建目录
        var dir = Path.GetDirectoryName(output);
        Directory.CreateDirectory(dir);
        //编译参数
        CSharpCompilationOptions option = null;
        if (isdebug)
        {
            option = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug, warningLevel: 4, allowUnsafe: true);
        }
        else
        {
            option = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release, warningLevel: 4, allowUnsafe: true);
        }

        //创建编译器代理
        var assemblyname = Path.GetFileNameWithoutExtension(output);
        var compilation = CSharpCompilation.Create(assemblyname, codes, assemblies, option);
        EmitResult result = null;
        if (!isdebug)
        {
            result = compilation.Emit(output);
        }
        else
        {
            var pdbPath = output + ".pdb";
            var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb,
                pdbFilePath: pdbPath);
            using (var dllStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                result = compilation.Emit(dllStream, pdbStream, options: emitOptions);

                File.WriteAllBytes(output, dllStream.GetBuffer());
                File.WriteAllBytes(pdbPath, pdbStream.GetBuffer());
            }
        }

        // 编译失败，提示
        if (!result.Success)
        {
            IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

            foreach (var diagnostic in failures)
            {
                Debug.LogError(diagnostic.ToString());
            }
        }
        return result.Success;
    }
}