"use client";

import { useEffect, useState } from "react";

type WaitlistSignalPayload = {
  count?: number;
  goal?: number;
};

type SignalState = {
  count: number;
  goal: number;
  loaded: boolean;
};

const initialState: SignalState = {
  count: 0,
  goal: 25,
  loaded: false,
};

export function announceWaitlistSignal(payload: WaitlistSignalPayload) {
  window.dispatchEvent(
    new CustomEvent<WaitlistSignalPayload>("momenttrace:waitlist-signal", {
      detail: payload,
    }),
  );
}

export default function WaitlistSignal() {
  const [signal, setSignal] = useState<SignalState>(initialState);

  useEffect(() => {
    let active = true;

    async function loadSignal() {
      try {
        const response = await fetch("/api/waitlist", {
          headers: { Accept: "application/json" },
        });
        const payload = (await response.json()) as WaitlistSignalPayload;

        if (!active || !response.ok) {
          return;
        }

        setSignal({
          count: payload.count ?? 0,
          goal: payload.goal ?? 25,
          loaded: true,
        });
      } catch {
        if (active) {
          setSignal((current) => ({ ...current, loaded: true }));
        }
      }
    }

    function handleSignalUpdate(event: Event) {
      const payload = (event as CustomEvent<WaitlistSignalPayload>).detail;

      setSignal((current) => ({
        count: payload.count ?? current.count,
        goal: payload.goal ?? current.goal,
        loaded: true,
      }));
    }

    void loadSignal();
    window.addEventListener("momenttrace:waitlist-signal", handleSignalUpdate);

    return () => {
      active = false;
      window.removeEventListener(
        "momenttrace:waitlist-signal",
        handleSignalUpdate,
      );
    };
  }, []);

  return (
    <span className="signal-number" aria-live="polite">
      {signal.loaded ? `${signal.count} / ${signal.goal}` : "... / 25"}
    </span>
  );
}
