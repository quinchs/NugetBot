using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot.Modules.TypeReaders
{
    public class DateTimeTypeReader : TypeReader
    {
        public static DateTimeTypeReader Instance
           => new DateTimeTypeReader();

        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (DateTime.TryParse(input, out var time))
                return TypeReaderResult.FromSuccess(time);

            return TypeReaderResult.FromError(ParseResult.FromError(CommandError.ParseFailed, "Input is not a date time"));
        }
    }
}
