using System.Linq;

namespace InventoryLedger
{
    internal static class UpdateConfiguration
    {
        public const string RepositoryOwner = "Roxyz0501";
        public const string RepositoryName = "inventory-ledger-act";
        public const string PluginAssemblyName = "InventoryLedger";
        public const string UpdaterAssemblyName = "InventoryLedger.Updater";
        public const string PluginFileName = "InventoryLedger.dll";
        public const string UpdaterFileName = "InventoryLedger.Updater.exe";

        public static bool IsConfigured => IsSafeSegment(RepositoryOwner) && IsSafeSegment(RepositoryName);

        public static string ReleasesApiUrl => "https://api.github.com/repos/" + RepositoryOwner + "/" + RepositoryName + "/releases?per_page=10";

        private static bool IsSafeSegment(string value) => !string.IsNullOrWhiteSpace(value) &&
            value.All(x => char.IsLetterOrDigit(x) || x == '-' || x == '_' || x == '.');
    }
}
