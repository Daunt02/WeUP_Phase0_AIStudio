import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "MapRadar",
  description: "Zero-scroll map experience",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="font-sans antialiased bg-[#050505]">
        {children}
      </body>
    </html>
  );
}
