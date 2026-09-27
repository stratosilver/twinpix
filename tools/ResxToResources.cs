// =====================================================================
//  Converts a .resx file into the binary .resources file that csc.exe
//  can embed with /resource. The .NET Framework ships the reader that
//  does the work (ResXResourceReader, in System.Windows.Forms.dll) and
//  the writer (ResourceWriter, in mscorlib), so build.bat compiles this
//  file with the very same csc.exe it uses for TwinPix itself: no SDK,
//  no resgen.exe, no download.
//
//  This file lives in tools\ on purpose. build.bat compiles the sources
//  of src\TwinPix\ only, and TwinPix.csproj lists its sources one by one, so
//  neither build pulls this converter into TwinPix.exe.
//
//  usage: ResxToResources <input.resx> <output.resources>
// =====================================================================
using System;
using System.Collections;
using System.IO;
using System.Resources;

internal static class ResxToResources
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: ResxToResources <input.resx> <output.resources>");
            return 2;
        }

        try
        {
            using (ResXResourceReader reader = new ResXResourceReader(args[0]))
            {
                // Lets the reader find files a .resx only points at
                // (ResXFileRef), which the designer writes for images
                // dropped on a form.
                reader.BasePath = Path.GetDirectoryName(Path.GetFullPath(args[0]));

                using (ResourceWriter writer = new ResourceWriter(args[1]))
                {
                    foreach (DictionaryEntry entry in reader)
                    {
                        string name = entry.Key as string;
                        if (name == null)
                        {
                            continue;
                        }

                        writer.AddResource(name, entry.Value);
                    }

                    writer.Generate();
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
