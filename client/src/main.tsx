import { StrictMode, useEffect, useState, type CSSProperties } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MotionConfig } from "motion/react";
import { DeviceMockup, iPhone16 } from "@mockifydev/react";

import "./config/maplibre";
import App from "./App";
import "./index.css";

const formatSystemTime = () =>
  new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date());

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
    },
  },
});

export function PhoneApp() {
  const calculateWidth = () => {
    const frameRatio = iPhone16.framePngWidth / iPhone16.framePngHeight;

    const widthFromHeight = window.innerHeight * 0.94 * frameRatio;

    const maxWidthFromViewport = window.innerWidth * 0.9;

    return Math.round(Math.min(widthFromHeight, maxWidthFromViewport));
  };

  const [phoneWidth, setPhoneWidth] = useState(calculateWidth);
  const [systemTime, setSystemTime] = useState(formatSystemTime);
  const phoneUiScale = (phoneWidth * iPhone16.screenWidthFraction) / 393;
  const statusBarHeight = Math.round(52 * phoneUiScale);
  const phoneHeight = phoneWidth * (iPhone16.framePngHeight / iPhone16.framePngWidth);

  useEffect(() => {
    const handleResize = () => {
      setPhoneWidth(calculateWidth());
    };

    window.addEventListener("resize", handleResize);
    const clockInterval = window.setInterval(() => {
      setSystemTime(formatSystemTime());
    }, 1_000);

    return () => {
      window.removeEventListener("resize", handleResize);
      window.clearInterval(clockInterval);
    };
  }, []);

  return (
    <main
      style={{
        width: "100vw",
        height: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        overflow: "hidden",
        background: "#000",
      }}
    >
      <div
        className="flowbb-device-wrap"
        style={{ width: phoneWidth, height: phoneHeight }}
      >
        <DeviceMockup
          device={iPhone16}
          color="White"
          width={phoneWidth}
          basePath="/mockify"
          showStatusBar={true}
          className="flowbb-device"
        >
          <div
            style={{
              width: "100%",
              height: "100%",
              overflow: "hidden",
              background: "#0a0a0a",
              position: "relative",
              transform: "translateZ(0)",
              "--phone-safe-top": `${statusBarHeight}px`,
              "--phone-safe-bottom": `${Math.round(24 * phoneUiScale)}px`,
            } as CSSProperties}
          >
            <App />
          </div>
        </DeviceMockup>
        <span
          aria-hidden="true"
          className="flowbb-system-time"
          style={{
            left:
              phoneWidth * iPhone16.screenLeftFraction +
              Math.round(28 * phoneUiScale),
            top: phoneHeight * iPhone16.screenTopFraction,
            width: Math.round(64 * phoneUiScale),
            height: statusBarHeight,
            fontSize: Math.round(15 * phoneUiScale),
          }}
        >
          {systemTime}
        </span>
      </div>
    </main>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <MotionConfig reducedMotion="user">
        <PhoneApp />
      </MotionConfig>
    </QueryClientProvider>
  </StrictMode>,
);
