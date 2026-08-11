import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "MomentTrace - Private Workday Memory Timeline",
  description:
    "MomentTrace is a local-first desktop memory timeline for apps, websites, screenshots, notes, and saved moments from your workday.",
  icons: {
    icon: "/favicon.svg",
    shortcut: "/favicon.svg",
  },
  openGraph: {
    title: "MomentTrace - Private Workday Memory Timeline",
    description:
      "A local-first desktop memory timeline for apps, websites, screenshots, notes, and saved moments from your workday.",
    images: ["/momenttrace-promotional-poster.png"],
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
