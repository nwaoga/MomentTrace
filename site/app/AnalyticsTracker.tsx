"use client";

import { useEffect } from "react";

const allowedEvents = new Set([
  "page_view",
  "join_waitlist_click",
  "see_timeline_click",
  "download_beta_click",
  "waitlist_submit_success",
]);

export function trackLandingEvent(eventName: string) {
  if (!allowedEvents.has(eventName)) {
    return;
  }

  const body = JSON.stringify({ eventName });

  if (navigator.sendBeacon) {
    const blob = new Blob([body], { type: "application/json" });
    navigator.sendBeacon("/api/analytics", blob);
    return;
  }

  void fetch("/api/analytics", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body,
    keepalive: true,
  });
}

export default function AnalyticsTracker() {
  useEffect(() => {
    trackLandingEvent("page_view");

    function handleClick(event: MouseEvent) {
      const target = event.target;

      if (!(target instanceof Element)) {
        return;
      }

      const tracked = target.closest<HTMLElement>("[data-track-event]");
      const eventName = tracked?.dataset.trackEvent;

      if (eventName) {
        trackLandingEvent(eventName);
      }
    }

    document.addEventListener("click", handleClick);
    return () => document.removeEventListener("click", handleClick);
  }, []);

  return null;
}
