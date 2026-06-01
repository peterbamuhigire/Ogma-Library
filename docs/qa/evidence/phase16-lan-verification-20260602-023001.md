# Phase 16 LAN Verification Evidence

| Field | Value |
| --- | --- |
| Generated UTC | 2026-06-01 23:34:59 |
| Repository | https://github.com/peterbamuhigire/Ogma-Library.git |
| Branch | main |
| Commit | e334fa0075db0e6bcfd1e859909e42dd41c85f69 |
| OS | Microsoft Windows 11 Pro 10.0.26200 build 26200 |
| .NET SDK | 10.0.101 |
| Verification skipped | False |

## Working Tree

```text
 M docs/developer-guide/images/scan-en.png
```

## Network Interfaces

| Name | Type | Status | IPv4 addresses |
| --- | --- | --- | --- |
| AVG Secure VPN Wintun | 53 | Down | 172.16.16.2 |
| Wi-Fi | Wireless80211 | Down | 169.254.42.228 |
| Local Area Connection* 1 | Wireless80211 | Down | 169.254.136.64 |
| Local Area Connection* 2 | Wireless80211 | Down | 169.254.12.100 |
| Ethernet | Ethernet | Up | 192.168.1.13 |
| VMware Network Adapter VMnet1 | Ethernet | Up | 192.168.253.1 |
| VMware Network Adapter VMnet8 | Ethernet | Up | 192.168.150.1 |
| Bluetooth Network Connection | Ethernet | Down | 169.254.211.246 |
| Loopback Pseudo-Interface 1 | Loopback | Up | 127.0.0.1 |

## Platform Probes

| Probe | Result |
| --- | --- |
| macOS Keychain service | Not applicable on this OS |

## Automated Verification

| Command | Exit code | Duration seconds |
| --- | ---: | ---: |
| `dotnet format OgmaLibrary.sln --verify-no-changes --no-restore` | 0 | 41.8 |
| `dotnet build OgmaLibrary.sln --configuration Release --no-restore` | 0 | 24.6 |
| `dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~LanHostScaffoldTests\|FullyQualifiedName~LanHostPersistenceTests\|FullyQualifiedName~LanHostCertificateProvisionerTests\|FullyQualifiedName~MdnsAdvertiserTests\|FullyQualifiedName~LanBindAddressSelectorTests\|FullyQualifiedName~LanClientAddressPolicyTests\|FullyQualifiedName~LanBookFileResolverTests\|FullyQualifiedName~LanPageRenderLimiterTests\|FullyQualifiedName~LanHostEndpointTests\|FullyQualifiedName~LanHostLoadSmokeTests\|FullyQualifiedName~HostSharingViewModelTests --logger console;verbosity=minimal` | 0 | 13.9 |
| `dotnet test tests/OgmaLibrary.Tests.Architecture/OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter FullyQualifiedName~ArchTests_LanHost\|FullyQualifiedName~ArchTests_StandaloneMode --logger console;verbosity=minimal` | 0 | 3.3 |

### dotnet format OgmaLibrary.sln --verify-no-changes --no-restore

```text
(no output)
```

### dotnet build OgmaLibrary.sln --configuration Release --no-restore

```text
  OgmaLibrary.Domain -> C:\wamp64\www\Ogma-Library\src\OgmaLibrary.Domain\bin\Release\net10.0\OgmaLibrary.Domain.dll
  OgmaLibrary.Application -> C:\wamp64\www\Ogma-Library\src\OgmaLibrary.Application\bin\Release\net10.0\OgmaLibrary.Application.dll
  OgmaLibrary.Reader -> C:\wamp64\www\Ogma-Library\src\OgmaLibrary.Reader\bin\Release\net10.0\OgmaLibrary.Reader.dll
  OgmaLibrary.Bookshelf3D -> C:\wamp64\www\Ogma-Library\src\OgmaLibrary.Bookshelf3D\bin\Release\net10.0\OgmaLibrary.Bookshelf3D.dll
  OgmaLibrary.Infrastructure -> C:\wamp64\www\Ogma-Library\src\OgmaLibrary.Infrastructure\bin\Release\net10.0\OgmaLibrary.Infrastructure.dll
  OgmaLibrary.Workers -> C:\wamp64\www\Ogma-Library\src\OgmaLibrary.Workers\bin\Release\net10.0\OgmaLibrary.Workers.dll
  OgmaLibrary.App -> C:\wamp64\www\Ogma-Library\src\OgmaLibrary.App\bin\Release\net10.0\OgmaLibrary.App.dll
  OgmaLibrary.Tests -> C:\wamp64\www\Ogma-Library\tests\OgmaLibrary.Tests\bin\Release\net10.0\OgmaLibrary.Tests.dll
  OgmaLibrary.Tests.Architecture -> C:\wamp64\www\Ogma-Library\tests\OgmaLibrary.Tests.Architecture\bin\Release\net10.0\OgmaLibrary.Tests.Architecture.dll
  OgmaLibrary.Tests.Ui -> C:\wamp64\www\Ogma-Library\tests\OgmaLibrary.Tests.Ui\bin\Release\net10.0\OgmaLibrary.Tests.Ui.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:24.17
```

### dotnet test tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~LanHostScaffoldTests|FullyQualifiedName~LanHostPersistenceTests|FullyQualifiedName~LanHostCertificateProvisionerTests|FullyQualifiedName~MdnsAdvertiserTests|FullyQualifiedName~LanBindAddressSelectorTests|FullyQualifiedName~LanClientAddressPolicyTests|FullyQualifiedName~LanBookFileResolverTests|FullyQualifiedName~LanPageRenderLimiterTests|FullyQualifiedName~LanHostEndpointTests|FullyQualifiedName~LanHostLoadSmokeTests|FullyQualifiedName~HostSharingViewModelTests --logger console;verbosity=minimal

```text
Test run for C:\wamp64\www\Ogma-Library\tests\OgmaLibrary.Tests\bin\Release\net10.0\OgmaLibrary.Tests.dll (.NETCoreApp,Version=v10.0)
VSTest version 18.0.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 10 s - OgmaLibrary.Tests.dll (net10.0)
```

### dotnet test tests/OgmaLibrary.Tests.Architecture/OgmaLibrary.Tests.Architecture.csproj --configuration Release --no-build --filter FullyQualifiedName~ArchTests_LanHost|FullyQualifiedName~ArchTests_StandaloneMode --logger console;verbosity=minimal

```text
Test run for C:\wamp64\www\Ogma-Library\tests\OgmaLibrary.Tests.Architecture\bin\Release\net10.0\OgmaLibrary.Tests.Architecture.dll (.NETCoreApp,Version=v10.0)
VSTest version 18.0.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 682 ms - OgmaLibrary.Tests.Architecture.dll (net10.0)
```

## Same-Subnet Verification

| Check | Evidence |
| --- | --- |
| Peer address/device |  |
| mDNS discovery from peer | Pending real same-subnet peer run |
| HTTPS health from peer | Pending real same-subnet peer run |
| Host CA Keychain verification | Not applicable on this Windows run; macOS runner/reference machine required |
| Notes | Generated from Windows development workspace after WP11 verification tooling landed; unrelated docs/developer-guide/images/scan-en.png remained dirty and unstaged. |

## Closeout Criteria

- Windows evidence must show the LAN Host automated tests, architecture guards, and same-subnet mDNS/HTTPS observations.
- macOS evidence must show the same LAN observations plus Host CA Keychain service evidence.
- If mDNS is blocked by a school firewall, record the failure and verify manual `ogma-lan://` join details against the same peer.
