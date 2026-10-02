using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Stokbox.Data.Migrations
{
    /// <summary>
    /// Reads the NNNN_name.sql scripts embedded in this assembly (folder Migrations).
    /// </summary>
    public static class EmbeddedMigrations
    {
        private static readonly Regex ResourceNamePattern = new Regex(
            @"^Stokbox\.Data\.Migrations\.(?<version>\d{4})_(?<name>.+)\.sql$",
            RegexOptions.CultureInvariant);

        public static IReadOnlyList<Migration> Load()
        {
            var assembly = typeof(EmbeddedMigrations).Assembly;
            var migrations = new List<Migration>();

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                var match = ResourceNamePattern.Match(resourceName);
                if (!match.Success)
                {
                    continue;
                }

                var version = int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);
                migrations.Add(new Migration(version, match.Groups["name"].Value, ReadResource(assembly, resourceName)));
            }

            return migrations.OrderBy(m => m.Version).ToList();
        }

        private static string ReadResource(Assembly assembly, string resourceName)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
