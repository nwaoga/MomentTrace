import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import test from "node:test";

const templateRoot = new URL("../", import.meta.url);

async function render() {
  const workerUrl = new URL("../dist/server/index.js", import.meta.url);
  workerUrl.searchParams.set("test", `${process.pid}-${Date.now()}`);
  const { default: worker } = await import(workerUrl.href);

  return worker.fetch(
    new Request("http://localhost/", {
      headers: { accept: "text/html" },
    }),
    {
      ASSETS: {
        fetch: async () => new Response("Not found", { status: 404 }),
      },
    },
    {
      waitUntil() {},
      passThroughOnException() {},
    },
  );
}

test("server-renders the MomentTrace landing page", async () => {
  const response = await render();
  assert.equal(response.status, 200);
  assert.match(response.headers.get("content-type") ?? "", /^text\/html\b/i);

  const html = await response.text();
  assert.match(html, /<title>MomentTrace - Private Workday Memory Timeline<\/title>/i);
  assert.match(html, /Recover the work you were just doing\./);
  assert.match(
    html,
    /Nothing leaves your machine unless you choose to export it\./,
  );
  assert.match(html, /Join the Signed-Build List/);
  assert.match(html, /25 named yeses/);
  assert.match(html, /momenttrace-overview-dashboard\.png/);
  assert.match(html, /momenttrace-overview-detail\.png/);
  assert.match(html, /momenttrace-timeline-detail\.png/);
  assert.doesNotMatch(html, /Your site is taking shape|react-loading-skeleton/);
});

test("keeps the launch page scoped to current assets and copy", async () => {
  const [page, layout, packageJson] = await Promise.all([
    readFile(new URL("../app/page.tsx", import.meta.url), "utf8"),
    readFile(new URL("../app/layout.tsx", import.meta.url), "utf8"),
    readFile(new URL("../package.json", import.meta.url), "utf8"),
  ]);

  assert.match(packageJson, /"name": "momenttrace-beta-site"/);
  assert.match(page, /Recover the work you were just doing\./);
  assert.match(page, /Nothing leaves your machine unless you choose to export it\./);
  assert.match(page, /data-track-event="join_waitlist_click"/);
  assert.match(layout, /title:\s*"MomentTrace - Private Workday Memory Timeline"/);
  assert.doesNotMatch(page, /desktop memory timeline|SkeletonPreview|_sites-preview/);
  assert.doesNotMatch(layout, /codex-preview|_sites-preview|themeColor|\bViewport\b/);

  await assert.rejects(
    access(new URL("app/_sites-preview", templateRoot)),
  );
  await assert.rejects(
    access(new URL("public/_sites-preview", templateRoot)),
  );
});
