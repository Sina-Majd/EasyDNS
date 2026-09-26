using EasyDNS.Core.Common;
using FluentAssertions;
using Xunit;

namespace EasyDNS.Tests
{
    public class IpValidatorTests
    {
        [Theory]
        [InlineData("1.1.1.1", true)]
        [InlineData("8.8.8.8", true)]
        [InlineData("208.67.222.222", true)]
        [InlineData("0.0.0.0", true)]
        [InlineData("255.255.255.255", true)]
        [InlineData("192.168.1.1", true)]
        [InlineData("1.1.1", false)]
        [InlineData("1.1.1.1.1", false)]
        [InlineData("256.0.0.1", false)]
        [InlineData("01.1.1.1", false)] // Leading zeros forbidden
        [InlineData("08.8.8.8", false)] // Leading zeros forbidden
        [InlineData("abc.def.ghi.jkl", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        [InlineData("1.1.1.1 ", true)] // Trimmed
        public void IsValidIpv4_ShouldValidateCorrectly(string? ip, bool expected)
        {
            bool actual = IpValidator.IsValidIpv4(ip);
            actual.Should().Be(expected);
        }

        [Theory]
        [InlineData("2606:4700:4700::1111", true)]
        [InlineData("2001:4860:4860::8888", true)]
        [InlineData("::1", true)]
        [InlineData("2001:db8:85a3::8a2e:370:7334", true)]
        [InlineData("1.1.1.1", false)]
        [InlineData("invalid_ipv6", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidIpv6_ShouldValidateCorrectly(string? ip, bool expected)
        {
            bool actual = IpValidator.IsValidIpv6(ip);
            actual.Should().Be(expected);
        }

        [Theory]
        [InlineData("1.1.1.1", true)]
        [InlineData("2606:4700:4700::1111", true)]
        [InlineData("invalid_ip", false)]
        public void IsValidIp_ShouldValidateBothIpv4AndIpv6(string? ip, bool expected)
        {
            bool actual = IpValidator.IsValidIp(ip);
            actual.Should().Be(expected);
        }
    }
}
