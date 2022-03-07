using System.Linq;
using System.Text;

public class StringHelpers
{
    public static string asciiBytesToString(byte[] chars)
    {
        return Encoding.ASCII.GetString(chars.TakeWhile(b => !b.Equals(0)).ToArray());
    }

    public static byte[] stringToAsciiBytes(string str)
    {
        return Encoding.ASCII.GetBytes(str + '\0');
    }

    public static bool nullsafeEqualityCheck(string str1, string str2)
    {
        // Treats null equal to blank string
        string unnulledStr1 = str1 == null ? "" : str1;
        string unnulledStr2 = str2 == null ? "" : str2;
        return unnulledStr1 == unnulledStr2;
    }
}


public class PathHelpers
{
    public static string GetApplicationPath()
    {
        string basePath = UnityEngine.Application.dataPath;
        if (basePath.EndsWith("Assets"))
        {
            // Returns Assets path if in IDE, so strip it
            basePath = basePath.Substring(0, basePath.Length - 6);
        }
        else
        {
            basePath += "/";
        }
        return basePath;
    }

}