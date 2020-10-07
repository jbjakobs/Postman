using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE80;

public static class DTEHandler
{
    [DllImport("ole32.dll")]
    private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);
    [DllImport("ole32.dll")]
    private static extern void GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

    public static DTE2 GetCurrent()
    {
        List<DTE2> list = new List<DTE2>();
        
        // Rot entry for any visual studio running
        //  Only one instance expected
        string rotEntryDTE = "VisualStudio.DTE";
        IRunningObjectTable rot;
        GetRunningObjectTable(0, out rot);
        IEnumMoniker enumMoniker;
        rot.EnumRunning(out enumMoniker);
        enumMoniker.Reset();
        IntPtr fetched = IntPtr.Zero;
        IMoniker[] moniker = new IMoniker[1];
        while (enumMoniker.Next(1, moniker, fetched) == 0)
        {
            IBindCtx bindCtx;
            CreateBindCtx(0, out bindCtx);
            string displayName;
            moniker[0].GetDisplayName(bindCtx, null, out displayName);
            if (displayName.Contains(rotEntryDTE))
            {
                object comObject;
                rot.GetObject(moniker[0], out comObject);
                list.Add((DTE2)(comObject));
            }
        }

        if (list.Count > 1) throw new Exception("Multiple VS running!");
        if (list.Count == 0) throw new Exception("No VS running!");
        return list[0];
    }
}