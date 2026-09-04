# Risks and Release-Gate Register

## Release blockers

| Risk / gate | Why it blocks release | Required evidence |
| --- | --- | --- |
| Native PDF sandbox/escape | Unsafe documents can escape process/resource boundaries | Windows/macOS hostile corpus, escape attempts, independent security review |
| Real extraction/OCR scale | Synthetic results do not predict production PDFs | Representative corpus, CPU/memory/throughput measurements, accepted budget |
| Provider terms/privacy | Third-party terms, retention, attribution, and regional availability are external obligations | Legal/privacy owner sign-off, retained policy snapshots, live network/config proof |
| Native 3D host | Headless bridge tests do not prove WebView2/WKWebView attachment | W-REF-01/M-REF-01 native integration and accessibility runs |
| Classroom deployment | LAN behavior depends on two hosts, firewall, discovery, trust, and reconnect | Two-machine test record, firewall/mDNS capture, hostile isolation and soak |
| Signing/install/rollback | An artifact is not a deliverable until trust and recovery are proven | Signed Windows/macOS artifacts, clean installs, interrupted upgrade, rollback |
| Backup/restore/owner approval | Data recovery and residual risk are operational decisions | Restore rehearsal, acceptance record, named owner and approval timestamp |

## Medium follow-ups

The Phase 37 safety scan records three conditional medium follow-ups and no
critical/high finding. They must remain tracked through the security review and
not be silently reclassified as closed by local test success.

## Release decision

Current decision: `NO-GO / CONTINUE VALIDATION`.

This is not a product-quality rejection. It reflects missing external and
physical evidence required by the canonical Phase 38/39 acceptance contract.
