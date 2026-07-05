using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YAXLib;

namespace LB_Mod_Installer.Tracking
{
    public class SavedSelectedOptions
    {
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Step")]
        public List<SavedInstallStep> SavedInstallSteps { get; set; } = new List<SavedInstallStep>();

        public static SavedSelectedOptions Load(string name)
        {
            string path = GetPath(name);

            if (!File.Exists(path))
            {
                return new SavedSelectedOptions();
            }

            try
            {
                YAXSerializer serializer = new YAXSerializer(typeof(SavedSelectedOptions), YAXSerializationOptions.DontSerializeNullObjects);
                var xml = (SavedSelectedOptions)serializer.DeserializeFromFile(path);

                if(xml.SavedInstallSteps == null)
                    xml.SavedInstallSteps = new List<SavedInstallStep>();

                foreach(SavedInstallStep step in xml.SavedInstallSteps)
                {
                    if (step.SelectedOptions == null)
                        step.SelectedOptions = new List<int>();
                }

                return xml;
            }
            catch
            {
                return new SavedSelectedOptions();
            }
        }

        public void Save(string name)
        {
            string path = GetPath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            try
            {
                if(!HasSavedOptions())
                {
                    //If no steps are being saved, then there is no reason to save this file at all. 

                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return;
                }

                YAXSerializer serializer = new YAXSerializer(typeof(SavedSelectedOptions));
                serializer.SerializeToFile(this, path);
            }
            catch { }
        }

        public bool HasSavedOptions()
        {
            if (SavedInstallSteps.Count == 0) return false;
            if (SavedInstallSteps.Any(x => x.SelectedOptions.Count > 0)) return true;
            return false;
        }

        public List<int> GetSelectedOptions(string stepId)
        {
            return SavedInstallSteps.FirstOrDefault(x => x.StepID == stepId)?.SelectedOptions;
        }

        public void SetSelectedOptions(string stepId, List<int> selectedOptions)
        {
            GetOrCreateStep(stepId).SelectedOptions = selectedOptions;
        }

        /// <summary>
        /// Returns the saved sub-choice index for each selected picker tile, keyed by option index.
        /// </summary>
        public Dictionary<int, int> GetSelectedChoices(string stepId)
        {
            return ParseChoices(SavedInstallSteps.FirstOrDefault(x => x.StepID == stepId)?.Choices);
        }

        public void SetSelectedChoices(string stepId, Dictionary<int, int> choices)
        {
            GetOrCreateStep(stepId).Choices = BuildChoices(choices);
        }

        private SavedInstallStep GetOrCreateStep(string stepId)
        {
            SavedInstallStep step = SavedInstallSteps.FirstOrDefault(x => x.StepID == stepId);

            if (step == null)
            {
                step = new SavedInstallStep();
                step.StepID = stepId;
                SavedInstallSteps.Add(step);
            }

            return step;
        }

        //Choices are stored compactly as "optionIndex:choiceIndex" pairs, comma separated.
        private static Dictionary<int, int> ParseChoices(string raw)
        {
            Dictionary<int, int> choices = new Dictionary<int, int>();
            if (string.IsNullOrWhiteSpace(raw)) return choices;

            foreach (string pair in raw.Split(','))
            {
                string[] parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int option) && int.TryParse(parts[1], out int choice))
                    choices[option] = choice;
            }

            return choices;
        }

        private static string BuildChoices(Dictionary<int, int> choices)
        {
            if (choices == null || choices.Count == 0) return null;
            return string.Join(",", choices.Select(x => $"{x.Key}:{x.Value}"));
        }

        private static string GetPath(string name)
        {
            return string.Format("{0}/LB Mod Installer 3/selected_options/{1}", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), name);
        }
    }

    [YAXSerializeAs("Step")]
    public class SavedInstallStep
    {
        [YAXAttributeForClass]
        public string StepID { get; set; }
        [YAXAttributeForClass]
        [YAXCollection(YAXCollectionSerializationTypes.Serially, SeparateBy = ",")]
        public List<int> SelectedOptions { get; set; } = new List<int>();
        [YAXAttributeForClass]
        [YAXDontSerializeIfNull]
        public string Choices { get; set; }
    }
}
