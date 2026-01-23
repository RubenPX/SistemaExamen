using System.Reflection.PortableExecutable;
using System.Threading.Channels;
using Examenes.Domain;
using StackExchange.Redis;

namespace Examenes.Server.BackgroundServices;

public static class ChannelManager {
    public static int MAX_SIGANLR_SIZE = 1_000_000;
    public static readonly Channel<AccionEvento> channelSIGANLR = Channel.CreateBounded<AccionEvento>(new BoundedChannelOptions(MAX_SIGANLR_SIZE) {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true
    });
}
