using UnityEditor;

public static class EditorUtils
{
    public static void AddDefineIfNecessary(string _define, BuildTargetGroup _buildTargetGroup)
    {
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(_buildTargetGroup);

        if (defines == null) { defines = _define; }
        else if (defines.Length == 0) { defines = _define; }
        else { if (defines.IndexOf(_define, 0) < 0) { defines += ";" + _define; } }

        PlayerSettings.SetScriptingDefineSymbolsForGroup(_buildTargetGroup, defines);
    }

    public static void RemoveDefineIfNecessary(string _define, BuildTargetGroup _buildTargetGroup)
    {
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(_buildTargetGroup);

        if (defines.StartsWith(_define + ";"))
        {
            // First of multiple defines.
            defines = defines.Remove(0, _define.Length + 1);
        }
        else if (defines.StartsWith(_define))
        {
            // The only define.
            defines = defines.Remove(0, _define.Length);
        }
        else if (defines.EndsWith(";" + _define))
        {
            // Last of multiple defines.
            defines = defines.Remove(defines.Length - _define.Length - 1, _define.Length + 1);
        }
        else
        {
            // Somewhere in the middle or not defined.
            var index = defines.IndexOf(_define, 0, System.StringComparison.Ordinal);
            if (index >= 0) { defines = defines.Remove(index, _define.Length + 1); }
        }

        PlayerSettings.SetScriptingDefineSymbolsForGroup(_buildTargetGroup, defines);
    }
}