import React, { useState, useEffect } from "react";
import { getVerificationQueue, verifyProvider, getCertificateUrl } from "./providersApi";
import LoadingSpinner from "../../shared/components/LoadingSpinner";
import ErrorMessage from "../../shared/components/ErrorMessage";
import AppButton from "../../shared/components/AppButton";
import { colors, typography } from "../../shared/theme";

const CATEGORY_COLORS = {
  Plumbing: { bg: "#e0f2fe", text: "#0369a1", border: "#bae6fd" },
  Electrical: { bg: "#fef3c7", text: "#b45309", border: "#fde68a" },
  "Vehicle Assistance": { bg: "#fee2e2", text: "#b91c1c", border: "#fecaca" },
  "Appliance Repair": { bg: "#ede9fe", text: "#6d28d9", border: "#ddd6fe" },
};

function ProviderVerificationQueue() {
  const [providers, setProviders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [processingId, setProcessingId] = useState(null);

  useEffect(() => {
    fetchQueue();
  }, []);

  const fetchQueue = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getVerificationQueue();
      setProviders(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.response?.data?.message || err.message || "Failed to load verification queue");
    } finally {
      setLoading(false);
    }
  };

  const handleVerify = async (providerId, isApproved) => {
    try {
      setProcessingId(providerId);
      // Pass enum name string matching backend serialization
      const status = isApproved ? "Verified" : "Rejected";
      await verifyProvider(providerId, status);
      setProviders((prev) => prev.filter((p) => p.providerId !== providerId));
    } catch (err) {
      alert(`Action failed: ${err.response?.data?.message || err.message}`);
    } finally {
      setProcessingId(null);
    }
  };

  const handleViewCertificate = (url) => {
    if (!url) {
      alert("No certificate file uploaded for this skill.");
      return;
    }
    const fileName = url.split(/[\\/]/).pop();
    const token = sessionStorage.getItem("assistlk_token");
    const fullUrl = getCertificateUrl(fileName);

    // Fetch as blob with Authorization header to view authenticated PDF stream
    fetch(fullUrl, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: Failed to stream certificate`);
        return res.blob();
      })
      .then((blob) => {
        const blobUrl = window.URL.createObjectURL(blob);
        window.open(blobUrl, "_blank", "noopener,noreferrer");
      })
      .catch((err) => {
        alert(err.message);
      });
  };

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="page-hero-content">
          <div>
            <div className="page-kicker">Component 2: Provider Management</div>
            <h1 className="page-hero-title">Provider Verification Queue</h1>
            <p className="page-hero-copy">
              Review technician credentials, verify background PDF certificates, and approve/reject provider onboarding requests.
            </p>
          </div>
          <div style={{ display: "flex", gap: "12px", alignItems: "center" }}>
            <AppButton variant="outline" onClick={fetchQueue}>
              Refresh Queue
            </AppButton>
          </div>
        </div>
      </section>

      {loading ? (
        <LoadingSpinner message="Loading pending provider applications..." />
      ) : error ? (
        <div className="glass-panel" style={{ padding: 24 }}>
          <ErrorMessage message={error} />
          <div style={{ marginTop: 16 }}>
            <AppButton variant="primary" onClick={fetchQueue}>
              Retry
            </AppButton>
          </div>
        </div>
      ) : (
        <div className="glass-panel" style={{ padding: 24, overflowX: "auto" }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: 16,
            }}
          >
            <h3 style={{ margin: 0, ...typography.cardHeading }}>
              Pending Applications ({providers.length})
            </h3>
          </div>

          {providers.length === 0 ? (
            <div style={{ textAlign: "center", padding: "48px 16px", color: colors.textSecondary }}>
              <div style={{ fontSize: "36px", marginBottom: "12px" }}>✅</div>
              <p style={{ ...typography.body, fontWeight: 600 }}>All applications reviewed</p>
              <p style={{ fontSize: "13px", color: "#64748b" }}>
                There are currently no provider onboarding requests awaiting verification.
              </p>
            </div>
          ) : (
            <table
              style={{
                width: "100%",
                borderCollapse: "separate",
                borderSpacing: "0 8px",
                textAlign: "left",
              }}
            >
              <thead>
                <tr style={{ color: colors.textSecondary, fontSize: "12px", textTransform: "uppercase", letterSpacing: "0.05em" }}>
                  <th style={{ padding: "12px 16px" }}>Provider & Contact</th>
                  <th style={{ padding: "12px 16px" }}>Business Name</th>
                  <th style={{ padding: "12px 16px" }}>Categories & Skills</th>
                  <th style={{ padding: "12px 16px" }}>Certificate</th>
                  <th style={{ padding: "12px 16px", textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {providers.map((provider) => (
                  <tr
                    key={provider.providerId}
                    style={{
                      backgroundColor: "rgba(255, 255, 255, 0.7)",
                      borderRadius: "8px",
                      boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
                    }}
                  >
                    <td style={{ padding: "16px", verticalAlign: "top" }}>
                      <div style={{ fontWeight: 600, color: colors.textPrimary, fontSize: "15px" }}>
                        {provider.fullName}
                      </div>
                      <div style={{ fontSize: "13px", color: colors.textSecondary, marginTop: "2px" }}>
                        📧 {provider.email}
                      </div>
                      <div style={{ fontSize: "13px", color: colors.textSecondary, marginTop: "2px" }}>
                        📞 {provider.phoneNumber || "Not provided"}
                      </div>
                      <div style={{ fontSize: "11px", color: "#94a3b8", marginTop: "4px" }}>
                        Submitted: {new Date(provider.createdAt).toLocaleDateString()}
                      </div>
                    </td>

                    <td style={{ padding: "16px", verticalAlign: "top", fontWeight: 500 }}>
                      {provider.businessName}
                    </td>

                    <td style={{ padding: "16px", verticalAlign: "top" }}>
                      {provider.skills?.map((skill, index) => {
                        const style = CATEGORY_COLORS[skill.category] || {
                          bg: "#f1f5f9",
                          text: "#334155",
                          border: "#cbd5e1",
                        };
                        return (
                          <div
                            key={index}
                            style={{
                              display: "inline-flex",
                              alignItems: "center",
                              gap: "6px",
                              backgroundColor: style.bg,
                              color: style.text,
                              border: `1px solid ${style.border}`,
                              padding: "4px 8px",
                              borderRadius: "6px",
                              fontSize: "12px",
                              fontWeight: 600,
                              marginRight: "6px",
                              marginBottom: "6px",
                            }}
                          >
                            <span>{skill.category}</span>
                            <span style={{ fontWeight: 400, color: "#475569" }}>• {skill.skillName}</span>
                          </div>
                        );
                      })}
                    </td>

                    <td style={{ padding: "16px", verticalAlign: "top" }}>
                      {provider.skills?.some((s) => s.certificationUrl) ? (
                        <button
                          type="button"
                          onClick={() => {
                            const cert = provider.skills.find((s) => s.certificationUrl);
                            handleViewCertificate(cert.certificationUrl);
                          }}
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "6px",
                            padding: "6px 12px",
                            backgroundColor: "#f8fafc",
                            border: "1px solid #cbd5e1",
                            borderRadius: "6px",
                            cursor: "pointer",
                            fontSize: "12px",
                            fontWeight: 600,
                            color: "#0f172a",
                          }}
                        >
                          📄 View / Download PDF
                        </button>
                      ) : (
                        <span style={{ fontSize: "12px", color: "#94a3b8" }}>None attached</span>
                      )}
                    </td>

                    <td style={{ padding: "16px", verticalAlign: "top", textAlign: "right" }}>
                      <div style={{ display: "inline-flex", gap: "8px" }}>
                        <button
                          type="button"
                          disabled={processingId === provider.providerId}
                          onClick={() => handleVerify(provider.providerId, true)}
                          style={{
                            padding: "8px 14px",
                            backgroundColor: "#16a34a",
                            color: "#ffffff",
                            border: "none",
                            borderRadius: "6px",
                            cursor: processingId === provider.providerId ? "not-allowed" : "pointer",
                            fontSize: "13px",
                            fontWeight: 600,
                            opacity: processingId === provider.providerId ? 0.6 : 1,
                          }}
                        >
                          Approve
                        </button>
                        <button
                          type="button"
                          disabled={processingId === provider.providerId}
                          onClick={() => handleVerify(provider.providerId, false)}
                          style={{
                            padding: "8px 14px",
                            backgroundColor: "#dc2626",
                            color: "#ffffff",
                            border: "none",
                            borderRadius: "6px",
                            cursor: processingId === provider.providerId ? "not-allowed" : "pointer",
                            fontSize: "13px",
                            fontWeight: 600,
                            opacity: processingId === provider.providerId ? 0.6 : 1,
                          }}
                        >
                          Reject
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}

export default ProviderVerificationQueue;
