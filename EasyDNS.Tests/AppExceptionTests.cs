using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace EasyDNS.Tests
{
    public class AppExceptionTests
    {
        [Fact]
        public async Task HandleFatalException_FromBackgroundThread_DoesNotThrowInvalidOperationException()
        {
            string? capturedMessage = null;
            App.FatalMessageBoxOverride = (msg, title) =>
            {
                capturedMessage = msg;
            };

            try
            {
                await Task.Run(() =>
                {
                    App.HandleFatalException(new InvalidOperationException("Test background crash"));
                });

                capturedMessage.Should().Be("Test background crash");
            }
            finally
            {
                App.FatalMessageBoxOverride = null;
            }
        }
    }
}
