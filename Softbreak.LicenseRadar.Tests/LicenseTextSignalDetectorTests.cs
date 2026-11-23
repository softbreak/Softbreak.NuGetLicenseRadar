using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseTextSignalDetectorTests
{
    private const string AutoMapperLicenseText = """
        By accessing code under the [Lucky Penny Software GitHub Organization](https://github.com/LuckyPennySoftware) (Lucky Penny Software) here, you are agreeing to the following licensing terms.
        If you do not agree to these terms, do not access Lucky Penny Software code.

        Your license to Lucky Penny Software source code and/or binaries is governed by the Reciprocal Public License 1.5 (RPL1.5) license as described here: 

        https://opensource.org/license/rpl-1-5/

        If you do not wish to release the source of software you build using Lucky Penny Software source code and/or binaries under the terms above, you may use Lucky Penny Software source code and/or binaries under the License Agreement described here:

        https://luckypennysoftware.com/license
        """;

    private const string MediatRLicenseText = AutoMapperLicenseText;

    private const string FluentAssertionsLicenseText = """
        XCEED SOFTWARE INC. 

        COMMUNITY LICENSE AGREEMENT 

        FLUENT ASSERTIONS 

        (for Non-Commercial Use) 

        GENERAL 

        This Community License Agreement (the "Agreement") is a legal agreement between you ("Licensee") and Xceed Software Inc. ("Xceed"), whereby Licensee wishes to use Fluent Assertions (the "Software"), an Xceed product, for Non-Commercial Use purposes. Xceed agrees to license its software to developers as along as all terms & conditions set forth herein are respected. The Software is provided under a license; it is not sold in any manner. By installing, copying or otherwise using the Software, you confirm your agreement to the terms and conditions expressed in this Agreement. If you do not agree, you are not authorized to use the Software. 

        DEFINITION OF NON-COMMERCIAL USE 

        For the purposes of this Agreement, Non-Commercial Use refers to any use of the Software where the primary objective is not to gain commercial advantage or monetary compensation or any other type of compensation or consideration. This includes, but is not limited to, the following: 

        The Software is used in developing or testing open-source software or open-source projects. 

        The Software is used for developing or testing personal, or experimental projects. 

        The Software is not being used by or for an organisation, group of persons, legal entity, or company, that charges fees or earns revenues with or without the intention to make profit. 

        The resulting work created or tested using the Software is not sold, leased, or sublicensed. 

        Any use outside of these parameters requires a paid Commercial License from Xceed.  

        LICENSE GRANT 

        Subject to compliance with the conditions set out below, Xceed grants to Licensee a non-exclusive and perpetual right (unless/until revoked by Xceed at its sole discretion) to install and use the Software for designing, building, testing (an application, system or program for Non-Commercial Use only, as defined above. 

        Would Licensee need to use the Software for any purpose that is not strictly Non-Commercial Use, Licensee must acquire a paid Commercial License.  
        """;

    private const string MitLicenseText = """
        MIT License

        Copyright (c) 2026 Example Author

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """;

    private const string Apache2LicenseText = """
        Apache License
        Version 2.0, January 2004
        http://www.apache.org/licenses/

        TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

        9. Accepting Warranty or Additional Liability. While redistributing
           the Work or Derivative Works thereof, You may choose to offer,
           and charge a fee for, acceptance of support, warranty, indemnity,
           or other liability obligations and/or rights consistent with this
           License. However, in accepting such obligations, You may act only
           on Your own behalf and on Your sole responsibility, not on behalf
           of any other Contributor, and only if You agree to indemnify,
           defend, and hold each Contributor harmless for any liability
           incurred by, or claims asserted against, such Contributor by reason
           of your accepting any such warranty or additional liability.
        """;

    private readonly LicenseTextSignalDetector _detector = new();

    [Fact]
    public void Detect_AutoMapper15ActualText_DetectsReciprocalAndAlternativeSignals()
    {
        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(AutoMapperLicenseText);

        Assert.Equal(2, signals.Count);

        LicenseTextSignal reciprocal = Assert.Single(
            signals,
            s => s.Kind == LicenseTextSignalKind.ReciprocalLicenseMention
        );
        Assert.Contains("Reciprocal Public License 1.5", reciprocal.EvidenceText);
        Assert.Equal("https://opensource.org/license/rpl-1-5/", reciprocal.EvidenceUrl);

        LicenseTextSignal alternative = Assert.Single(
            signals,
            s => s.Kind == LicenseTextSignalKind.AlternativeLicenseAgreement
        );
        Assert.Contains("License Agreement described here", alternative.EvidenceText);
        Assert.Equal("https://luckypennysoftware.com/license", alternative.EvidenceUrl);

        Assert.DoesNotContain(signals, s => s.Kind == LicenseTextSignalKind.PaidCommercialLicenseMention);
    }

    [Fact]
    public void Detect_MediatR13ActualText_DetectsReciprocalAndAlternativeSignals()
    {
        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(MediatRLicenseText);

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, s => s.Kind == LicenseTextSignalKind.ReciprocalLicenseMention);
        Assert.Contains(signals, s => s.Kind == LicenseTextSignalKind.AlternativeLicenseAgreement);
    }

    [Fact]
    public void Detect_FluentAssertions8ActualText_DetectsPaidCommercialSignal()
    {
        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(FluentAssertionsLicenseText);

        LicenseTextSignal signal = Assert.Single(signals);
        Assert.Equal(LicenseTextSignalKind.PaidCommercialLicenseMention, signal.Kind);
        Assert.Contains("requires a paid Commercial License", signal.EvidenceText);
        Assert.DoesNotContain(signals, s => s.Kind == LicenseTextSignalKind.ReciprocalLicenseMention);
        Assert.DoesNotContain(signals, s => s.Kind == LicenseTextSignalKind.AlternativeLicenseAgreement);
    }

    [Fact]
    public void Detect_MitLicense_ReturnsNoSignals()
    {
        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(MitLicenseText);

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_Apache2License_ReturnsNoSignals()
    {
        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(Apache2LicenseText);

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_AmbiguousContextFreeKeywords_ReturnsNoSignals()
    {
        const string text = """
            This library is open source. Commercial use is permitted without fee under MIT.
            Paid support or enterprise consulting services may be offered separately.
            Dealing in the software without restriction including rights to sell copies.
            """;

        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(text);

        Assert.Empty(signals);
    }

    [Fact]
    public void Detect_NegatedCommercialRequirement_ReturnsNoSignals()
    {
        const string text = """
            No paid commercial license is required to use this software in production.
            You may use it for commercial use without a paid license.
            """;

        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(text);

        Assert.Empty(signals);
    }

    [Theory]
    [InlineData("A paid Commercial License is not required for this project.")]
    [InlineData("The commercial license is optional for all users.")]
    [InlineData("No paid commercial license is necessary for this software.")]
    public void Detect_NegatedOrOptionalLicense_DoesNotRaisePaidSignal(string text)
    {
        Assert.DoesNotContain(_detector.Detect(text),
            signal => signal.Kind == LicenseTextSignalKind.PaidCommercialLicenseMention);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n \t  ")]
    public void Detect_NullOrWhitespace_ReturnsEmpty(string? input)
    {
        IReadOnlyList<LicenseTextSignal> signals = _detector.Detect(input);

        Assert.Empty(signals);
    }
}
