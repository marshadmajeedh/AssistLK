import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import useGeolocation from "../hooks/useGeolocation";
import serviceRequestService from "../services/serviceRequestService";
import { canEditRequest } from "../utils/serviceRequestStatus";
import getApiErrorMessage from "../utils/getApiErrorMessage";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import AppInput from "../../../shared/components/AppInput";
import AppTextArea from "../../../shared/components/AppTextArea";
import StatusBadge from "../../../shared/components/StatusBadge";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import LoadingSpinner from "../../../shared/components/LoadingSpinner";
import { colors, radius, spacing, typography } from "../../../shared/theme";

const MAX_DESCRIPTION_LENGTH = 4000;
const MAX_LOCATION_LENGTH = 255;

function formatCoordinate(coord) {
  if (coord === null || coord === undefined) {
    return null;
  }
  const num = Number(coord);
  if (Number.isNaN(num)) {
    return String(coord);
  }
  return num.toFixed(5);
}

function EditServiceRequestPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  // Geolocation hook for capturing new GPS coordinates on demand
  const {
    accuracy: geoAccuracy,
    loading: geoLoading,
    error: geoError,
    getCurrentLocation,
    clearLocation: clearGeoHook,
  } = useGeolocation();

  // Request & form states
  const [request, setRequest] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);

  const [description, setDescription] = useState("");
  const [locationText, setLocationText] = useState("");
  const [latitude, setLatitude] = useState(null);
  const [longitude, setLongitude] = useState(null);
  const [accuracy, setAccuracy] = useState(null);

  const [errors, setErrors] = useState({});
  const [apiError, setApiError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Fetch initial request data
  useEffect(() => {
    let isMounted = true;

    async function fetchRequest() {
      setLoading(true);
      setLoadError(null);

      try {
        const data = await serviceRequestService.getById(id);
        if (!isMounted) return;

        setRequest(data);
        setDescription(data.description || "");
        setLocationText(data.locationText || "");

        // Initialize form coordinates directly from persisted backend request
        // Distinguish persisted coordinates so initial null state of useGeolocation does NOT erase them
        if (
          data.latitude !== null &&
          data.latitude !== undefined &&
          data.longitude !== null &&
          data.longitude !== undefined
        ) {
          const latNum = Number(data.latitude);
          const lngNum = Number(data.longitude);
          setLatitude(Number.isNaN(latNum) ? data.latitude : latNum);
          setLongitude(Number.isNaN(lngNum) ? data.longitude : lngNum);
        } else {
          setLatitude(null);
          setLongitude(null);
        }
      } catch (err) {
        if (!isMounted) return;
        const message = getApiErrorMessage(
          err,
          "Failed to load service request details."
        );
        setLoadError(message);
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    if (id) {
      fetchRequest();
    }

    return () => {
      isMounted = false;
    };
  }, [id]);

  const handleDescriptionChange = (e) => {
    setDescription(e.target.value);
    if (errors.description) {
      setErrors((prev) => ({ ...prev, description: null }));
    }
  };

  const handleLocationChange = (e) => {
    setLocationText(e.target.value);
    if (errors.locationText) {
      setErrors((prev) => ({ ...prev, locationText: null }));
    }
  };

  // Capture new GPS coordinates and update form state
  const handleFetchGps = async () => {
    if (errors.coordinates) {
      setErrors((prev) => ({ ...prev, coordinates: null }));
    }

    const pos = await getCurrentLocation();
    if (pos && typeof pos.latitude === "number" && typeof pos.longitude === "number") {
      setLatitude(pos.latitude);
      setLongitude(pos.longitude);
      setAccuracy(pos.accuracy ?? null);
    }
  };

  // Explicitly clear GPS coordinates from form state
  const handleClearGps = () => {
    clearGeoHook();
    setLatitude(null);
    setLongitude(null);
    setAccuracy(null);
    if (errors.coordinates) {
      setErrors((prev) => ({ ...prev, coordinates: null }));
    }
  };

  const validate = () => {
    const validationErrors = {};

    const trimmedDescription = description.trim();
    if (!trimmedDescription) {
      validationErrors.description =
        "Please describe the problem you need assistance with.";
    } else if (description.length > MAX_DESCRIPTION_LENGTH) {
      validationErrors.description = `Description cannot exceed ${MAX_DESCRIPTION_LENGTH} characters.`;
    }

    const trimmedLocation = locationText.trim();
    if (!trimmedLocation) {
      validationErrors.locationText = "Please enter your location.";
    } else if (locationText.length > MAX_LOCATION_LENGTH) {
      validationErrors.locationText = `Location cannot exceed ${MAX_LOCATION_LENGTH} characters.`;
    }

    const hasLat = latitude !== null && latitude !== undefined;
    const hasLng = longitude !== null && longitude !== undefined;

    if (hasLat !== hasLng) {
      validationErrors.coordinates =
        "Latitude and longitude must be supplied together.";
    } else if (hasLat && hasLng) {
      const latNum = Number(latitude);
      const lngNum = Number(longitude);

      if (typeof latNum !== "number" || Number.isNaN(latNum) || latNum < -90 || latNum > 90) {
        validationErrors.coordinates =
          "Latitude must be a valid number between -90 and 90.";
      } else if (typeof lngNum !== "number" || Number.isNaN(lngNum) || lngNum < -180 || lngNum > 180) {
        validationErrors.coordinates =
          "Longitude must be a valid number between -180 and 180.";
      }
    }

    setErrors(validationErrors);
    return Object.keys(validationErrors).length === 0;
  };

  const handleSubmit = async (event) => {
    event.preventDefault();

    if (isSubmitting) return;

    setApiError(null);

    if (!validate()) {
      return;
    }

    setIsSubmitting(true);

    const requestId = request.serviceRequestId ?? request.id ?? id;

    // Payload EXACTLY: description, locationText, latitude, longitude
    const payload = {
      description: description.trim(),
      locationText: locationText.trim(),
      latitude:
        latitude !== null && latitude !== undefined ? Number(latitude) : null,
      longitude:
        longitude !== null && longitude !== undefined ? Number(longitude) : null,
    };

    try {
      await serviceRequestService.update(requestId, payload);
      navigate(`/service-requests/${requestId}`);
    } catch (err) {
      const message = getApiErrorMessage(
        err,
        "Failed to update service request. Please check your information and try again."
      );
      setApiError(message);
      setIsSubmitting(false);
    }
  };

  // Loading state
  if (loading) {
    return (
      <div className="narrow-stack" style={{ width: "100%" }}>
        <LoadingSpinner message="Loading service request..." />
      </div>
    );
  }

  // Load error state
  if (loadError || !request) {
    return (
      <div className="feature-stack narrow-stack" style={{ width: "100%" }}>
        <AppCard className="section-card">
          <h2
            style={{
              ...typography.sectionHeading,
              margin: 0,
              color: colors.textPrimary,
            }}
          >
            Unable to Edit Request
          </h2>
          <ErrorMessage
            message={
              loadError ||
              "The requested service request was not found or cannot be edited."
            }
          />
          <div style={{ marginTop: spacing.lg }}>
            <AppButton
              variant="outline"
              type="button"
              onClick={() => navigate("/service-requests")}
            >
              ← Back to My Requests
            </AppButton>
          </div>
        </AppCard>
      </div>
    );
  }

  const requestId = request.serviceRequestId ?? request.id ?? id;
  const isEditable = canEditRequest(request.status);

  // Invalid edit state guard: Request cannot be edited in current status
  if (!isEditable) {
    return (
      <div className="feature-stack narrow-stack" style={{ width: "100%" }}>
        <AppCard className="section-card">
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              flexWrap: "wrap",
              gap: spacing.sm,
              marginBottom: spacing.md,
            }}
          >
            <h2
              style={{
                ...typography.sectionHeading,
                margin: 0,
                color: colors.textPrimary,
              }}
            >
              Request Not Editable
            </h2>
            <StatusBadge status={request.status} />
          </div>

          <p
            style={{
              ...typography.body,
              color: colors.textSecondary,
              margin: `0 0 ${spacing.lg}px 0`,
            }}
          >
            This request can no longer be edited in its current status.
          </p>

          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: spacing.md,
            }}
          >
            <AppButton
              type="button"
              variant="outline"
              onClick={() => navigate(`/service-requests/${requestId}`)}
            >
              ← Back to Request
            </AppButton>

            <AppButton
              type="button"
              variant="outline"
              onClick={() => navigate("/service-requests")}
            >
              My Requests
            </AppButton>
          </div>
        </AppCard>
      </div>
    );
  }

  const hasCoordinates =
    latitude !== null &&
    latitude !== undefined &&
    longitude !== null &&
    longitude !== undefined;

  const currentAccuracy = accuracy ?? geoAccuracy;

  return (
    <div
      className="feature-stack narrow-stack"
      style={{
        width: "100%",
        boxSizing: "border-box",
      }}
    >
      <section className="page-hero">
        <div className="page-hero-content">
          <div>
            <div className="page-kicker">Request Update</div>
            <h1 className="page-hero-title">Refine the details before the workflow moves forward.</h1>
            <p className="page-hero-copy">Update the description, location, or GPS information while this request is still editable.</p>
          </div>
          <div className="page-hero-meta">
            <div className="page-stat">
              <strong>{request.status}</strong>
              <span>Current lifecycle status</span>
            </div>
          </div>
        </div>
      </section>

      <AppCard className="section-card">
        <form onSubmit={handleSubmit} noValidate>
          {/* Problem Description Field */}
          <div>
            <AppTextArea
              id="edit-service-request-description"
              label="Problem Description *"
              value={description}
              onChange={handleDescriptionChange}
              placeholder="Provide details about the service or repair you need..."
              rows={5}
              disabled={isSubmitting}
              error={errors.description}
            />

            <div
              style={{
                display: "flex",
                justifyContent: "flex-end",
                marginTop: spacing.xs,
              }}
            >
              <span
                style={{
                  ...typography.small,
                  color:
                    description.length > MAX_DESCRIPTION_LENGTH
                      ? colors.error
                      : colors.textSecondary,
                }}
              >
                {description.length} / {MAX_DESCRIPTION_LENGTH}
              </span>
            </div>
          </div>

          {/* Location Field */}
          <div style={{ marginTop: spacing.md }}>
            <AppInput
              id="edit-service-request-location"
              label="Location *"
              value={locationText}
              onChange={handleLocationChange}
              placeholder="e.g. 45 Galle Road, Colombo 03"
              disabled={isSubmitting}
              error={errors.locationText}
            />
          </div>

          {/* GPS Location Section */}
          <div
            style={{
              marginTop: spacing.lg,
              padding: spacing.md,
              backgroundColor: colors.background,
              borderRadius: radius.medium,
              border: `1px solid ${colors.border}`,
            }}
          >
            <div
              style={{
                ...typography.cardHeading,
                color: colors.textPrimary,
                marginBottom: spacing.xs,
              }}
            >
              GPS Location (Optional)
            </div>

            <p
              style={{
                ...typography.small,
                color: colors.textSecondary,
                margin: `0 0 ${spacing.md}px 0`,
              }}
            >
              Updating your GPS coordinates can help match nearby service
              providers accurately.
            </p>

            {hasCoordinates ? (
              <div>
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: spacing.xs,
                    marginBottom: spacing.xs,
                  }}
                >
                  <span
                    style={{
                      ...typography.body,
                      fontWeight: 600,
                      color: colors.success,
                    }}
                  >
                    GPS coordinates configured
                  </span>
                </div>

                <div
                  style={{
                    ...typography.small,
                    color: colors.textPrimary,
                    display: "flex",
                    flexDirection: "column",
                    gap: 2,
                  }}
                >
                  <div>
                    <span style={{ fontWeight: 600 }}>Latitude:</span>{" "}
                    {formatCoordinate(latitude)}
                  </div>
                  <div>
                    <span style={{ fontWeight: 600 }}>Longitude:</span>{" "}
                    {formatCoordinate(longitude)}
                  </div>
                  {currentAccuracy !== null && currentAccuracy !== undefined && (
                    <div style={{ color: colors.textSecondary }}>
                      Accuracy: approximately {Math.round(currentAccuracy)} metres
                    </div>
                  )}
                </div>

                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: spacing.sm,
                    marginTop: spacing.md,
                  }}
                >
                  <AppButton
                    type="button"
                    variant="outline"
                    onClick={handleFetchGps}
                    disabled={geoLoading || isSubmitting}
                    style={{
                      minHeight: 36,
                      padding: `0 ${spacing.sm}px`,
                      fontSize: typography.small.fontSize,
                    }}
                  >
                    {geoLoading
                      ? "Updating location..."
                      : "Update to Current Location"}
                  </AppButton>

                  <AppButton
                    type="button"
                    variant="outline"
                    onClick={handleClearGps}
                    disabled={isSubmitting}
                    style={{
                      minHeight: 36,
                      padding: `0 ${spacing.sm}px`,
                      fontSize: typography.small.fontSize,
                    }}
                  >
                    Clear GPS Location
                  </AppButton>
                </div>
              </div>
            ) : (
              <div>
                <AppButton
                  type="button"
                  variant="outline"
                  onClick={handleFetchGps}
                  disabled={geoLoading || isSubmitting}
                  style={{
                    minHeight: 40,
                    fontSize: typography.small.fontSize,
                  }}
                >
                  {geoLoading
                    ? "Detecting location..."
                    : "Use Current Location"}
                </AppButton>
              </div>
            )}

            {/* Geolocation warning/error */}
            {geoError && (
              <div
                style={{
                  ...typography.small,
                  color: colors.warning,
                  backgroundColor: colors.warningLight,
                  padding: spacing.sm,
                  borderRadius: radius.small,
                  marginTop: spacing.sm,
                }}
              >
                {geoError}
              </div>
            )}

            {/* Coordinates client validation error */}
            {errors.coordinates && (
              <div
                style={{
                  ...typography.small,
                  color: colors.error,
                  marginTop: spacing.sm,
                }}
              >
                {errors.coordinates}
              </div>
            )}
          </div>

          {/* API Error Message */}
          <ErrorMessage message={apiError} />

          {/* Form Actions */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "flex-end",
              gap: spacing.md,
              marginTop: spacing.xl,
            }}
          >
            <AppButton
              type="button"
              variant="outline"
              onClick={() => navigate(`/service-requests/${requestId}`)}
              disabled={isSubmitting}
            >
              Cancel
            </AppButton>

            <AppButton type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Saving changes..." : "Save Changes"}
            </AppButton>
          </div>
        </form>
      </AppCard>
    </div>
  );
}

export default EditServiceRequestPage;
