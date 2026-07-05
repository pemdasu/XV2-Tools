using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xv2CoreLib.Resource;

namespace LB_Mod_Installer.Installer
{
    /// <summary>
    /// Loads modular .installoption tiles into Picker steps. A Picker step names an ImportFolder,
    /// and every .installoption file found under data/&lt;folder&gt;/ or JUNGLE3/&lt;folder&gt;/ becomes a tile.
    /// </summary>
    public static class OptionModuleImporter
    {
        public const string Extension = ".installoption";

        private static readonly string[] Roots = { "data", "JUNGLE3" };

        public static void ImportModules(InstallerXml installerXml, ZipReader zipManager)
        {
            if (installerXml?.InstallOptionSteps == null) return;

            foreach (InstallStep step in installerXml.InstallOptionSteps)
            {
                if (step.StepType != InstallStep.StepTypes.Picker) continue;
                if (string.IsNullOrWhiteSpace(step.ImportFolder)) continue;

                if (step.OptionList == null)
                    step.OptionList = new List<InstallOption>();

                foreach (string entryPath in FindModulePaths(zipManager, step.ImportFolder))
                {
                    InstallOption option;

                    try
                    {
                        option = zipManager.DeserializeXmlFromArchive_Ext<InstallOption>(entryPath);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Failed to load the option module \"{entryPath}\".\n\n{ex.Message}", ex);
                    }

                    if (option != null)
                        step.OptionList.Add(option);
                }
            }
        }

        private static List<string> FindModulePaths(ZipReader zipManager, string importFolder)
        {
            string folder = importFolder.Trim().Trim('/');

            //Match .installoption files directly inside data/<folder>/ or JUNGLE3/<folder>/ (any depth below the folder).
            List<string> prefixes = Roots.Select(root => $"{root}/{folder}/").ToList();

            return zipManager.archive.Entries
                .Select(entry => entry.FullName)
                .Where(fullName =>
                    fullName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) &&
                    prefixes.Any(prefix => fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(fullName => fullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
