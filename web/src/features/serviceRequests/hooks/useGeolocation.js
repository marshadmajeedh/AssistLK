import { useCallback, useState } from "react";

export function useGeolocation() {
  const [latitude, setLatitude] = useState(null);
  const [longitude, setLongitude] = useState(null);
  const [accuracy, setAccuracy] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const getCurrentLocation = useCallback(() => {
    return new Promise((resolve) => {
      if (typeof window === "undefined" || !navigator.geolocation) {
        const errorMsg =
          "Geolocation is not supported by your browser. Please enter your location manually.";
        setError(errorMsg);
        setLoading(false);
        resolve(null);
        return;
      }

      setLoading(true);
      setError(null);

      const options = {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 0,
      };

      navigator.geolocation.getCurrentPosition(
        (position) => {
          const lat = position.coords.latitude;
          const lng = position.coords.longitude;
          const acc = position.coords.accuracy;

          setLatitude(lat);
          setLongitude(lng);
          setAccuracy(acc);
          setLoading(false);
          setError(null);

          resolve({
            latitude: lat,
            longitude: lng,
            accuracy: acc,
          });
        },
        (geoError) => {
          let message =
            "Failed to retrieve location. Please enter your location manually.";

          switch (geoError.code) {
            case 1: // PERMISSION_DENIED
              message =
                "Location permission was denied. Please allow access in browser settings or enter your location manually.";
              break;
            case 2: // POSITION_UNAVAILABLE
              message =
                "Location information is currently unavailable. Please enter your location manually.";
              break;
            case 3: // TIMEOUT
              message =
                "Location request timed out. Please try again or enter your location manually.";
              break;
            default:
              if (geoError.message) {
                message = geoError.message;
              }
              break;
          }

          setError(message);
          setLoading(false);
          resolve(null);
        },
        options
      );
    });
  }, []);

  const clearLocation = useCallback(() => {
    setLatitude(null);
    setLongitude(null);
    setAccuracy(null);
    setLoading(false);
    setError(null);
  }, []);

  return {
    latitude,
    longitude,
    accuracy,
    loading,
    error,
    getCurrentLocation,
    clearLocation,
  };
}

export default useGeolocation;
