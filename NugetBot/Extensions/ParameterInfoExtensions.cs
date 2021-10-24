using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetBot
{
    public static class ParameterInfoExtensions
    {
        public static async Task HandleErrorAsync(this ParameterInfo info, DualCommandContext context, object input)
        {
            var handlerAttribute = (ParseErrorHandlerAttribute)info.Attributes.FirstOrDefault(x => x.GetType() == typeof(ParseErrorHandlerAttribute));

            if (handlerAttribute == null)
                return;

            await handlerAttribute.Handler.ExecuteAsync(context, info, input).ConfigureAwait(false);
        }
    }
}
