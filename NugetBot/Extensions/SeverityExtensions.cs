using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class SeverityExtensions
    {
        public static Severity ToLogSeverity(this LogSeverity sev)
        {
            return sev switch
            {
                LogSeverity.Critical => Severity.Critical,
                LogSeverity.Debug => Severity.Debug,
                LogSeverity.Error => Severity.Error,
                LogSeverity.Info => Severity.Log,
                LogSeverity.Verbose => Severity.Verbose,
                LogSeverity.Warning => Severity.Warning,
                _ => Severity.Log
            };
        }
    }
}
