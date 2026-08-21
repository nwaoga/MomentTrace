"use client";

import { FormEvent, useState } from "react";
import { trackLandingEvent } from "./AnalyticsTracker";
import { announceWaitlistSignal } from "./WaitlistSignal";

type SubmitState =
  | { status: "idle"; message: string }
  | { status: "submitting"; message: string }
  | { status: "success"; message: string }
  | { status: "error"; message: string };

export default function WaitlistForm() {
  const [state, setState] = useState<SubmitState>({
    status: "idle",
    message: "",
  });

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);

    setState({
      status: "submitting",
      message: "Adding you to the signed-build list...",
    });

    try {
      const response = await fetch("/api/waitlist", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: data.get("name"),
          email: data.get("email"),
          platform: data.get("platform"),
          useCase: data.get("useCase"),
          source: "public_landing_page",
          website: data.get("website"),
        }),
      });
      const payload = (await response.json()) as {
        count?: number;
        error?: string;
        goal?: number;
      };

      if (!response.ok) {
        setState({
          status: "error",
          message: payload.error ?? "Something went wrong. Try again in a moment.",
        });
        return;
      }

      form.reset();
      trackLandingEvent("waitlist_submit_success");
      announceWaitlistSignal({
        count: payload.count,
        goal: payload.goal,
      });
      setState({
        status: "success",
        message: `You're on the list. Current signal: ${payload.count ?? 0} / ${
          payload.goal ?? 25
        } named requests.`,
      });
    } catch {
      setState({
        status: "error",
        message: "Could not reach the waitlist. Please try again in a moment.",
      });
    }
  }

  return (
    <form className="waitlist-form" onSubmit={handleSubmit}>
      <label>
        <span>Name</span>
        <input name="name" autoComplete="name" required />
      </label>
      <label>
        <span>Email</span>
        <input name="email" type="email" autoComplete="email" required />
      </label>
      <label>
        <span>Platform</span>
        <select name="platform" defaultValue="windows" required>
          <option value="windows">Windows</option>
          <option value="macos">macOS</option>
          <option value="both">Both</option>
        </select>
      </label>
      <label>
        <span>What do you want MomentTrace for?</span>
        <textarea name="useCase" rows={4} required />
      </label>
      <label className="hidden-field" aria-hidden="true">
        <span>Website</span>
        <input name="website" tabIndex={-1} autoComplete="off" />
      </label>
      <button
        className="button primary"
        type="submit"
        disabled={state.status === "submitting"}
      >
        {state.status === "submitting" ? "Joining..." : "Join the List"}
      </button>
      {state.message ? (
        <p className={`form-message ${state.status}`}>{state.message}</p>
      ) : null}
    </form>
  );
}
