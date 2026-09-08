import { useState } from "react";
import { useNavigate } from "react-router-dom";
import useGeolocation from "../hooks/useGeolocation";
import serviceRequestService from "../services/serviceRequestService";
import getApiErrorMessage from "../utils/getApiErrorMessage";
import AppButton from "../../../shared/components/AppButton";
import AppCard from "../../../shared/components/AppCard";
import AppInput from "../../../shared/components/AppInput";
import AppTextArea from "../../../shared/components/AppTextArea";
import ErrorMessage from "../../../shared/components/ErrorMessage";
import { colors, radius, spacing, typography } from "../../../shared/theme";

const MAX_DESCRIPTION_LENGTH = 4000;
const MAX_LOCATION_LENGTH = 255;

function CreateServiceRequestPage() {
  const navigate = useNavigate();
  const {
    latitude,
    longitude,
    accuracy,
    loading: geoLoading,
    error: geoError,
    getCurrentLocation,
    clearLocation,
  } = useGeolocation();

  const [description, setDescription] = useState("");
  const [locationText, setLocationText] = useState("");
  const [errors, setErrors] = useState({});
  const [apiError, setApiError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleDescriptionChange = (e) => {
    const value = e.target.value;
    setDescription(value);
    if (errors.description) {
      setErrors((prev) => ({ ...prev, description: null }));
    }
  };

  const handleLocationChange = (e) => {
    const value = e.target.value;
    setLocationText(value);
    if (errors.locationText) {
      setErrors((prev) => ({ ...prev, locationText: null }));
    }
  };

  const handleFetchGps = async () => {
    if (errors.coordinates) {
      setErrors((prev) => ({ ...prev, coordinates: null }));
    }
    await getCurrentLocation();
  };

  const handleClearGps = () => {
    clearLocation();
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

    // Coordinate validation if GPS coordinates are partially or wholly provided
    const hasLat = latitude !== null && latitude !== undefined;
    const hasLng = longitude !== null && longitude !== undefined;

    if (hasLat !== hasLng) {
      validationErrors.coordinates =
        "Latitude and longitude must be supplied together.";
    } else if (hasLat && hasLng) {
      if (
        typeof latitude !== "number" ||
        Number.isNaN(latitude) ||
        latitude < -90 ||
        latitude > 90
      ) {
        validationErrors.coordinates =
          "Latitude must be a valid number between -90 and 90.";
      } else if (
        typeof longitude !== "number" ||
        Number.isNaN(longitude) ||
        longitude < -180 ||
        longitude > 180
      ) {
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

    // Exact payload: raw numeric coordinates or null
    const payload = {
      description: description.trim(),
      locationText: locationText.trim(),
      latitude: latitude !== null && latitude !== undefined ? latitude : null,
      longitude: longitude !== null && longitude !== undefined ? longitude : null,
    };

    try {
      await serviceRequestService.create(payload);
      // On success, navigate to /service-requests
      navigate("/service-requests");
    } catch (err) {
      const message = getApiErrorMessage(
        err,
        "Failed to create service request. Please check your information and try again."
      );
      setApiError(message);
      setIsSubmitting(false);
    }
  };

  const isCoordinatesCaptured =
    latitude !== null &&
    latitude !== undefined &&
    longitude !== null &&
    longitude !== undefined;

  return (
    <div
      style={{
        maxWidth: 680,
        margin: "0 auto",
        width: "100%",
        boxSizing: "border-box",
      }}
    >
      {/* Page Header */}
      <div style={{ marginBottom: spacing.lg }}>
        <h1
          style={{
            ...typography.pageTitle,
            margin: 0,
            color: colors.textPrimary,
          }}
        >
          Create Service Request
        </h1>
        <p
          style={{
            ...typography.body,
            margin: `${spacing.xs}px 0 0 0`,
            color: colors.textSecondary,
          }}
        >
          Describe the problem you need help with and provide your location.
        </p>
      </div>

      <AppCard>
        <form onSubmit={handleSubmit} noValidate>
          {/* Problem Description Field */}
          <div>
            <AppTextArea
              id="service-request-description"
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
              id="service-request-location"
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
              Adding your current location can help find nearby service
              providers.
            </p>

            {/* Use Current Location button */}
            {!isCoordinatesCaptured && (
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
                {geoLoading ? "Detecting location..." : "Use Current Location"}
              </AppButton>
            )}

            {/* Geolocation capture success display */}
            {isCoordinatesCaptured && (
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
                    Current location captured
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
                    {typeof latitude === "number"
                      ? latitude.toFixed(5)
                      : latitude}
                  </div>
                  <div>
                    <span style={{ fontWeight: 600 }}>Longitude:</span>{" "}
                    {typeof longitude === "number"
                      ? longitude.toFixed(5)
                      : longitude}
                  </div>
                  {accuracy !== null && accuracy !== undefined && (
                    <div style={{ color: colors.textSecondary }}>
                      Accuracy: approximately {Math.round(accuracy)} metres
                    </div>
                  )}
                </div>

                <div style={{ marginTop: spacing.sm }}>
                  <AppButton
                    type="button"
                    variant="outline"
                    onClick={handleClearGps}
                    disabled={isSubmitting}
                    style={{
                      minHeight: 34,
                      padding: `0 ${spacing.sm}px`,
                      fontSize: typography.small.fontSize,
                    }}
                  >
                    Clear GPS Location
                  </AppButton>
                </div>
              </div>
            )}

            {/* Geolocation friendly error */}
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
              onClick={() => navigate("/service-requests")}
              disabled={isSubmitting}
            >
              Cancel
            </AppButton>

            <AppButton type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Creating request..." : "Create Request"}
            </AppButton>
          </div>
        </form>
      </AppCard>
    </div>
  );
}

export default CreateServiceRequestPage;
