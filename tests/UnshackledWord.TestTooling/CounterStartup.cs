using Xunit.Sdk;
using Xunit.v3;

namespace UnshackledWord.TestTooling;

//https://youtu.be/qIMRFKHldvQ
public class CounterStartup : ITestPipelineStartup
{
    public ValueTask StartAsync(IMessageSink diagnosticMessageSink)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync()
    {
        return ValueTask.CompletedTask;
    }
}
