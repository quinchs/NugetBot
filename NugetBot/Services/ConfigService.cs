using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NugetBot
{
    public class Config
    {
        [ConfigSummary("The token of the bot.")]
        public string Token { get; set; } = "BOT_TOKEN_HERE";

        [ConfigSummary("The Mongo connection string.")]
        public string MongoCS { get; set; } = "MONGO_CS_HERE";
    }


    /// <summary>
    ///     Represents a static service to interact with a config file.
    /// </summary>
    public class ConfigService
    {
        /// <summary>
        ///     The location of the config file relative to the current directory.
        /// </summary>
        public const string ConfigPath = @"./Config.jsonc";

        /// <summary>
        ///     The currently loaded config.
        /// </summary>
        public static Config Config { get; private set; }

        /// <summary>
        ///     Loads the configuration file, setting the <see cref="Config"/> field.
        /// </summary>
        public static void LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                var c = new Config();
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(c, Formatting.Indented));

                throw new FileNotFoundException("No config file found, please make a config file in the current directory!");
            }

            var json = File.ReadAllText(ConfigPath);

            Config = JsonConvert.DeserializeObject<Config>(json);

            if(Config == null)
            {
                var c = new Config();
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(c));

                throw new FileNotFoundException("No config file found, please make a config file in the current directory!");
            }

            // Save it to add any unadded properties
            File.WriteAllText(ConfigPath, CompileConfig(Config ?? new Config()));
        }

        private static string CompileConfig(Config conf)
        {
            string json = JsonConvert.SerializeObject(conf, Formatting.Indented);

            var props = conf.GetType().GetProperties().Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ConfigSummary)));

            foreach (var prop in props)
            {
                var s = prop.GetCustomAttribute<ConfigSummary>().Summary;

                json = Regex.Replace(json, @$"""({prop.Name})"":", m =>
                {
                    // count whitespaces
                    int ws = 0;
                    for (int i = m.Index - 1; i != 0; i--)
                    {
                        char c = json[i];
                        if (c != ' ' && c != '\\')
                            break;
                        ws++;
                    }

                    return $"// {s}\n{" ".PadRight(ws)}{m.Value}";
                });
            }

            return json;
        }

        public static void Modify(Action<Config> func)
        {
            func(Config);

            SaveConfig(Config);
        }

        /// <summary>
        ///     Saves the provided config class to the config file.
        /// </summary>
        /// <param name="conf">The new config class to save</param>
        public static void SaveConfig(Config conf)
        {
            var json = JsonConvert.SerializeObject(conf, Formatting.Indented);

            File.WriteAllText(ConfigPath, json);

            Config = conf;
        }
    }

    public class ConfigSummary : Attribute
    {
        public readonly string Summary;


        public ConfigSummary(string summary)
        {
            this.Summary = summary;
        }
    }
}
