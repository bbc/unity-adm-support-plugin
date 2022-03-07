using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Packaging
{

    public static void buildPackage()
    {
        var outputPackage = GetArg("-outputPackage");
        if(outputPackage == null)
        {
            outputPackage = "Package.unityPackage";
        }

        var exportedPackageAssetList = new List<string>();

        exportedPackageAssetList.Add("Assets/UnityAdm");
        AssetDatabase.ExportPackage(exportedPackageAssetList.ToArray(), outputPackage,
            ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

    }

    //getting arguments from command line by argument name;
    private static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}