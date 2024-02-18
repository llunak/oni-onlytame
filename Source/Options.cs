using PeterHan.PLib.Options;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace OnlyTame
{
    [JsonObject(MemberSerialization.OptIn)]
    [ModInfo("https://github.com/llunak/oni-onlytame")]
    [ConfigFile(SharedConfigLocation: true)]
    [RestartRequired]
    public sealed class Options : SingletonOptions< Options >, IOptions
    {
        [Option("Add Filter to Conveyor Loader", "Add the option to limit to tame only to the conveyor loader.")]
        [JsonProperty]
        public bool AddFilterToConveyorLoader { get; set; } = false;

        [Option("Add Filter to Automatic Dispenser", "Add the option to limit to tame only to the automatic dispenser.")]
        [JsonProperty]
        public bool AddFilterToAutomaticDispenser { get; set; } = false;

        public override string ToString()
        {
            return $"ShowLiquidOnAirflowTiles.Options[addfiltertoconveyorloader={AddFilterToConveyorLoader},"
                + $"addfiltertoautomaticdispenser={AddFilterToAutomaticDispenser}]";
        }

        public void OnOptionsChanged()
        {
            // 'this' is the Options instance used by the options dialog, so set up
            // the actual instance used by the mod. MemberwiseClone() is enough to copy non-reference data.
            Instance = (Options) this.MemberwiseClone();
        }

        public IEnumerable<IOptionsEntry> CreateOptions()
        {
            return null;
        }
    }
}
