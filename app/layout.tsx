import type { Metadata } from "next";
import { Inter } from "next/font/google";
import TelemetryBootstrap from "@/components/TelemetryBootstrap";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-sans" });

export const metadata: Metadata = {
  title: "Mapbox Explorer",
  description: "Interactive Mapbox integration",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={inter.variable}>
      <body className="font-sans antialiased">
        <TelemetryBootstrap />
        {children}
      </body>
    </html>
  );
}
